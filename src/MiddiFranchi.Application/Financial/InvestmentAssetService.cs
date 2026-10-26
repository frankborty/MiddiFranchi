using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Comandi del catalogo strumenti e delle operazioni d'investimento. Le operazioni vengono
/// validate sull'intera catena, così una modifica non può rendere invalida una vendita successiva.
/// </summary>
public sealed class InvestmentAssetService
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly IInstrumentRepository _instruments;
    private readonly IFinancialOperationRepository _operations;
    private readonly IPriceObservationRepository _prices;
    private readonly ICategoryRepository _categories;
    private readonly ITransactionRunner _transactions;
    private readonly Func<DateOnly> _currentUtcDate;

    public InvestmentAssetService(
        IInstrumentRepository instruments,
        IFinancialOperationRepository operations,
        IPriceObservationRepository prices,
        ICategoryRepository categories,
        ITransactionRunner transactions,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _instruments = instruments;
        _operations = operations;
        _prices = prices;
        _categories = categories;
        _transactions = transactions;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<Instrument> CreateAsync(
        NewInvestmentAsset request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var instrument = new Instrument(
            Guid.NewGuid(),
            request.Name,
            request.AssetClass,
            Euro,
            request.PriceUnit,
            request.NotionalValue,
            request.Symbol,
            request.Isin,
            request.ReferenceUrl,
            request.Notes,
            InstrumentStatus.Active,
            request.MaturityDate,
            request.CouponRate,
            request.TaxRate);

        await EnsureUniqueIdentityAsync(instrument, excludingId: null, cancellationToken);
        await _instruments.AddAsync(instrument, cancellationToken);
        return instrument;
    }

    public async Task<Instrument> UpdateAsync(
        UpdateInvestmentAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await RequireInstrumentAsync(request.InstrumentId, cancellationToken);
        await EnsureValuationShapeEditableAsync(
            existing,
            request.PriceUnit,
            request.NotionalValue,
            cancellationToken);

        var updated = new Instrument(
            existing.Id,
            request.Name,
            request.AssetClass,
            existing.QuoteCurrency,
            request.PriceUnit,
            request.NotionalValue,
            request.Symbol,
            request.Isin,
            request.ReferenceUrl,
            request.Notes,
            existing.Status,
            request.MaturityDate,
            request.CouponRate,
            request.TaxRate);

        await EnsureUniqueIdentityAsync(updated, excludingId: existing.Id, cancellationToken);
        await _instruments.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<Instrument> ArchiveAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var instrument = await RequireInstrumentAsync(instrumentId, cancellationToken);
        if (instrument.IsArchived)
        {
            return instrument;
        }

        var held = await HeldQuantityAsync(instrumentId, cancellationToken);
        if (held > 0m)
        {
            throw new InvalidOperationException(
                "Vendi tutte le quote prima di archiviare lo strumento.");
        }

        var archived = instrument.WithStatus(InstrumentStatus.Archived);
        await _instruments.UpdateAsync(archived, cancellationToken);
        return archived;
    }

    public async Task<Instrument> ReactivateAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var instrument = await RequireInstrumentAsync(instrumentId, cancellationToken);
        if (instrument.IsActive)
        {
            return instrument;
        }

        var active = instrument.WithStatus(InstrumentStatus.Active);
        await _instruments.UpdateAsync(active, cancellationToken);
        return active;
    }

    public async Task DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var instrument = await RequireInstrumentAsync(instrumentId, cancellationToken);

        if (await _operations.AnyForInstrumentAsync(instrumentId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Lo strumento «{instrument.Name}» ha operazioni registrate: archivialo invece di eliminarlo.");
        }

        if (await _prices.AnyForInstrumentAsync(instrumentId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Lo strumento «{instrument.Name}» ha quotazioni registrate: archivialo invece di eliminarlo.");
        }

        await _instruments.DeleteAsync(instrumentId, cancellationToken);
    }

    public async Task<InvestmentOperation> RegisterOperationAsync(
        RegisterInvestmentOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.Kind);

        var instrument = await RequireActiveInstrumentAsync(request.InstrumentId, cancellationToken);

        var operation = BuildOperation(
            Guid.NewGuid(),
            instrument,
            settlementAccount: null,
            request.Kind,
            request.OperationDate,
            DateTimeOffset.UtcNow,
            request.Quantity,
            request.Price,
            request.FeeAmount,
            request.Amount,
            request.Description);

        await EnsureChainValidAsync(
            operation,
            excludedOperationId: null,
            cancellationToken);

        return await _transactions.RunAsync(async token =>
        {
            await _operations.AddAsync(operation, token);
            await SaveTaxExpenseAsync(operation, request.TaxAmount, token);
            await SaveFeeExpenseAsync(operation, request.FeeAmount, token);
            if (operation.InvestmentKind == InvestmentOperationKind.Sell)
            {
                await ArchiveIfFullySoldAsync(instrument.Id, token);
            }

            return operation;
        }, cancellationToken);
    }

    /// <summary>
    /// Importa operazioni storiche (acquisti e vendite) mantenendo gli identificativi forniti.
    /// È pensato per la migrazione da altre applicazioni: accetta strumenti archiviati e conti
    /// chiusi, valida l'intera catena una sola volta e inserisce le operazioni in blocco.
    /// </summary>
    public async Task<IReadOnlyList<InvestmentOperation>> ImportOperationsAsync(
        IReadOnlyList<ImportInvestmentOperationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        var operations = new List<InvestmentOperation>(requests.Count);
        foreach (var item in requests)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.Operation.Kind is not (
                    InvestmentOperationKind.Buy or
                    InvestmentOperationKind.Sell))
            {
                throw new ArgumentException(
                    "L'importazione supporta solo acquisti e vendite.",
                    nameof(requests));
            }

            var instrument = await RequireInstrumentAsync(
                    item.Operation.InstrumentId,
                    cancellationToken)
                .ConfigureAwait(false);

            operations.Add(BuildOperation(
                item.OperationId,
                instrument,
                settlementAccount: null,
                item.Operation.Kind,
                item.Operation.OperationDate,
                item.RecordedAt,
                item.Operation.Quantity,
                item.Operation.Price,
                item.Operation.FeeAmount,
                item.Operation.Amount,
                item.Operation.Description));
        }

        var chain = (await _operations.ListAsync(cancellationToken).ConfigureAwait(false))
            .OfType<InvestmentOperation>()
            .ToList();
        chain.AddRange(operations);

        InvestmentPositionCalculator.Positions(chain, DateOnly.MaxValue);
        InvestmentCostBasisCalculator.Calculate(chain, DateOnly.MaxValue);

        foreach (var operation in operations)
        {
            await _operations.AddAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        foreach (var operation in operations.Where(operation =>
                     operation.InvestmentKind is
                         InvestmentOperationKind.Buy or
                         InvestmentOperationKind.Sell))
        {
            var fee = operation.Fees
                .Where(f => f.Kind is FeeKind.BuyFee or FeeKind.SellFee)
                .Sum(f => f.Amount.Value.Amount);
            await SaveFeeExpenseAsync(operation, fee > 0m ? fee : null, cancellationToken)
                .ConfigureAwait(false);
        }

        await ArchiveSoldOutAsync(
                operations
                    .Where(operation => operation.InvestmentKind == InvestmentOperationKind.Sell)
                    .Select(operation => operation.Instrument!.Id),
                cancellationToken)
            .ConfigureAwait(false);

        return operations;
    }

    /// <summary>
    /// Archivia gli strumenti indicati che non hanno più quote detenute. È usato anche
    /// dalle reimportazioni, dove le operazioni possono essere già presenti e quindi saltate.
    /// </summary>
    public async Task ArchiveSoldOutAsync(
        IEnumerable<Guid> instrumentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrumentIds);

        foreach (var instrumentId in instrumentIds.Distinct())
        {
            await ArchiveIfFullySoldAsync(instrumentId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<InvestmentOperation> UpdateOperationAsync(
        UpdateInvestmentOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await RequireEditableInvestmentAsync(request.OperationId, cancellationToken);
        var instrument = existing.Instrument
            ?? throw new InvalidOperationException("L'operazione non è collegata a uno strumento.");
        if (instrument.IsArchived)
        {
            throw new InvalidOperationException(
                "Lo strumento è archiviato: riattivalo prima di modificare le operazioni.");
        }

        var updated = BuildOperation(
            existing.Id,
            instrument,
            settlementAccount: null,
            existing.InvestmentKind,
            request.OperationDate,
            existing.RecordedAt,
            request.Quantity,
            request.Price,
            request.FeeAmount,
            request.Amount,
            request.Description);

        await EnsureChainValidAsync(
            updated,
            excludedOperationId: existing.Id,
            cancellationToken);

        return await _transactions.RunAsync(async token =>
        {
            await _operations.UpdateInvestmentAsync(updated, token);
            await SaveTaxExpenseAsync(updated, request.TaxAmount, token);
            await SaveFeeExpenseAsync(updated, request.FeeAmount, token);
            if (updated.InvestmentKind == InvestmentOperationKind.Sell)
            {
                await ArchiveIfFullySoldAsync(instrument.Id, token);
            }

            return updated;
        }, cancellationToken);
    }

    public async Task DeleteOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var existing = await RequireEditableInvestmentAsync(operationId, cancellationToken);
        if (existing.Instrument is { IsArchived: true })
        {
            throw new InvalidOperationException(
                "Lo strumento è archiviato: riattivalo prima di modificare le operazioni.");
        }

        await EnsureChainValidAsync(
            candidate: null,
            excludedOperationId: existing.Id,
            cancellationToken);

        await _transactions.RunAsync(async token =>
        {
            await DeleteLinkedExpensesAsync(existing, token);
            await _operations.DeleteAsync(operationId, token);
            return true;
        }, cancellationToken);
    }

    private Task SaveTaxExpenseAsync(
        InvestmentOperation operation,
        decimal? taxAmount,
        CancellationToken cancellationToken)
    {
        if (operation.InvestmentKind is not (
            InvestmentOperationKind.Sell or
            InvestmentOperationKind.Dividend or
            InvestmentOperationKind.Coupon))
        {
            return Task.CompletedTask;
        }

        var description = operation.InvestmentKind == InvestmentOperationKind.Sell
            ? "Tasse su plusvalenze"
            : "Tasse su cedola";

        return SaveLinkedExpenseAsync(
            InvestmentExpenses.TaxIdForOperation(operation.Id),
            operation.OperationDate,
            operation.RecordedAt,
            taxAmount,
            "Tasse Investimenti",
            description,
            operation.Instrument,
            cancellationToken);
    }

    private Task SaveFeeExpenseAsync(
        InvestmentOperation operation,
        decimal? feeAmount,
        CancellationToken cancellationToken)
    {
        if (operation.InvestmentKind is not (
            InvestmentOperationKind.Buy or
            InvestmentOperationKind.Sell))
        {
            return Task.CompletedTask;
        }

        var description = operation.InvestmentKind == InvestmentOperationKind.Buy
            ? "Commissioni su acquisto"
            : "Commissioni su vendita";

        return SaveLinkedExpenseAsync(
            InvestmentExpenses.FeeIdForOperation(operation.Id),
            operation.OperationDate,
            operation.RecordedAt,
            feeAmount,
            "Conto Titoli",
            description,
            operation.Instrument,
            cancellationToken);
    }

    /// <summary>
    /// Imposta o rimuove la ritenuta collegata a una cedola registrata come entrata.
    /// </summary>
    public async Task SaveIncomeTaxAsync(
        Guid incomeId,
        decimal? taxAmount,
        CancellationToken cancellationToken = default)
    {
        var operation = await _operations.GetByIdAsync(incomeId, cancellationToken)
            ?? throw new InvalidOperationException("Operazione non trovata.");
        if (operation is not Income income)
        {
            throw new InvalidOperationException("L'operazione non è un'entrata.");
        }

        await SaveLinkedExpenseAsync(
                InvestmentExpenses.TaxIdForOperation(income.Id),
                income.OperationDate,
                income.RecordedAt,
                taxAmount,
                "Tasse Investimenti",
                "Tasse su cedola",
                income.Instrument,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SaveLinkedExpenseAsync(
        Guid expenseId,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        decimal? amount,
        string subcategory,
        string description,
        Instrument? instrument,
        CancellationToken cancellationToken)
    {
        var existing = await _operations.GetByIdAsync(expenseId, cancellationToken) as Expense;
        if (amount is null or <= 0m)
        {
            if (existing is not null)
            {
                await _operations.DeleteAsync(existing.Id, cancellationToken);
            }

            return;
        }

        var category = await RequireExpenseCategoryAsync(subcategory, cancellationToken);
        var money = new PositiveMoney(new Money(amount.Value, Euro));

        if (existing is not null)
        {
            var updated = new Expense(
                existing.Id,
                account: null,
                category,
                money,
                operationDate,
                recordedAt,
                description,
                notes: null,
                instrument: instrument);
            await _operations.UpdateAsync(updated, cancellationToken);
        }
        else
        {
            var expense = new Expense(
                expenseId,
                account: null,
                category,
                money,
                operationDate,
                recordedAt,
                description,
                notes: null,
                instrument: instrument);
            await _operations.AddAsync(expense, cancellationToken);
        }
    }

    private async Task<Category> RequireExpenseCategoryAsync(
        string subcategory,
        CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(cancellationToken);
        return categories.FirstOrDefault(candidate =>
            candidate.Type == CategoryType.Expense &&
            string.Equals(candidate.Name, "Fees", StringComparison.CurrentCultureIgnoreCase) &&
            string.Equals(candidate.Subcategory, subcategory, StringComparison.CurrentCultureIgnoreCase))
            ?? throw new InvalidOperationException(
                $"La categoria Fees · {subcategory} non esiste.");
    }

    private async Task DeleteLinkedExpensesAsync(
        InvestmentOperation operation,
        CancellationToken cancellationToken)
    {
        await DeleteExpenseIfExistsAsync(
            InvestmentExpenses.TaxIdForOperation(operation.Id),
            cancellationToken);
        await DeleteExpenseIfExistsAsync(
            InvestmentExpenses.FeeIdForOperation(operation.Id),
            cancellationToken);
    }

    private async Task DeleteExpenseIfExistsAsync(
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        if (await _operations.GetByIdAsync(expenseId, cancellationToken) is not null)
        {
            await _operations.DeleteAsync(expenseId, cancellationToken);
        }
    }

    public async Task<PriceObservation> RecordPriceAsync(
        RecordPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Price <= 0m)
        {
            throw new ArgumentException("La quotazione deve essere positiva.", nameof(request));
        }

        var today = _currentUtcDate();
        if (request.ValidAt > today)
        {
            throw new ArgumentException(
                $"La data della quotazione non può essere successiva a oggi ({today:yyyy-MM-dd}).",
                nameof(request));
        }

        var instrument = await RequireInstrumentAsync(request.InstrumentId, cancellationToken);
        var source = string.IsNullOrWhiteSpace(request.Source) ? "Manuale" : request.Source.Trim();
        var observedAt = DateTimeOffset.UtcNow;

        var observation = instrument.PriceUnit switch
        {
            PriceUnit.PerUnit => PriceObservation.AtUnitPrice(
                Guid.NewGuid(),
                instrument,
                new UnitPrice(new Money(request.Price, instrument.QuoteCurrency)),
                request.ValidAt,
                observedAt,
                source),
            PriceUnit.PercentOfNominal => PriceObservation.AsPercentOfNominal(
                Guid.NewGuid(),
                instrument,
                request.Price,
                request.ValidAt,
                observedAt,
                source),
            _ => throw new InvalidOperationException(
                $"L'unità di prezzo {instrument.PriceUnit} non è supportata."),
        };

        await _prices.AddAsync(observation, cancellationToken);
        return observation;
    }

    private InvestmentOperation BuildOperation(
        Guid id,
        Instrument instrument,
        Account? settlementAccount,
        InvestmentOperationKind kind,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        decimal? quantity,
        decimal? price,
        decimal? feeAmount,
        decimal? amount,
        string? description)
    {
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        return kind switch
        {
            InvestmentOperationKind.Buy => InvestmentOperation.Buy(
                id,
                instrument,
                settlementAccount,
                RequireQuantity(quantity),
                BuildQuotedPrice(instrument, price),
                BuildFees(kind, feeAmount, settlementAccount),
                operationDate,
                recordedAt,
                cleanDescription,
                null),
            InvestmentOperationKind.Sell => InvestmentOperation.Sell(
                id,
                instrument,
                settlementAccount,
                RequireQuantity(quantity),
                BuildQuotedPrice(instrument, price),
                BuildFees(kind, feeAmount, settlementAccount),
                operationDate,
                recordedAt,
                cleanDescription,
                null),
            InvestmentOperationKind.Dividend => InvestmentOperation.Dividend(
                id,
                instrument,
                settlementAccount,
                BuildAmount(amount, settlementAccount),
                operationDate,
                recordedAt,
                cleanDescription,
                null,
                BuildFees(kind, feeAmount, settlementAccount)),
            InvestmentOperationKind.Coupon => InvestmentOperation.Coupon(
                id,
                instrument,
                settlementAccount,
                BuildAmount(amount, settlementAccount),
                operationDate,
                recordedAt,
                cleanDescription,
                null,
                BuildFees(kind, feeAmount, settlementAccount)),
            _ => throw new ArgumentException(
                $"Il tipo di operazione {kind} non è supportato dalla pagina Investimenti.",
                nameof(kind)),
        };
    }

    private async Task EnsureUniqueIdentityAsync(
        Instrument instrument,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        if (instrument.Isin is { } isin &&
            await _instruments.IsinExistsAsync(isin, excludingId, cancellationToken))
        {
            throw new InvalidOperationException($"Esiste già uno strumento con ISIN {isin}.");
        }

        if (instrument.Symbol is { } symbol &&
            await _instruments.SymbolExistsAsync(symbol, excludingId, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Esiste già uno strumento con la sigla {symbol}.");
        }
    }

    private async Task EnsureValuationShapeEditableAsync(
        Instrument existing,
        PriceUnit priceUnit,
        Money? notionalValue,
        CancellationToken cancellationToken)
    {
        var changed = existing.PriceUnit != priceUnit || existing.NotionalValue != notionalValue;
        if (!changed)
        {
            return;
        }

        if (await _operations.AnyForInstrumentAsync(existing.Id, cancellationToken))
        {
            throw new InvalidOperationException(
                "Non puoi modificare unità di prezzo o valore nominale dopo aver registrato operazioni.");
        }

        if (await _prices.AnyForInstrumentAsync(existing.Id, cancellationToken))
        {
            throw new InvalidOperationException(
                "Non puoi modificare unità di prezzo o valore nominale dopo aver registrato quotazioni.");
        }
    }

    private async Task<Instrument> RequireInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        return await _instruments.GetByIdAsync(instrumentId, cancellationToken)
            ?? throw new InvalidOperationException($"Lo strumento {instrumentId} non esiste.");
    }

    private async Task<Instrument> RequireActiveInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var instrument = await RequireInstrumentAsync(instrumentId, cancellationToken);
        if (instrument.IsArchived)
        {
            throw new InvalidOperationException(
                "Lo strumento è archiviato: riattivalo prima di registrare operazioni.");
        }

        return instrument;
    }

    private async Task<InvestmentOperation> RequireEditableInvestmentAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        var operation = await _operations.GetByIdAsync(operationId, cancellationToken);
        if (operation is not InvestmentOperation investment)
        {
            throw new InvalidOperationException("L'operazione non è un'operazione d'investimento.");
        }

        if (investment.Status != OperationStatus.Registered)
        {
            throw new InvalidOperationException(
                "L'operazione è stornata e non può essere modificata o eliminata da questa pagina.");
        }

        var allOperations = await _operations.ListAsync(cancellationToken);
        if (allOperations.OfType<ReversalOperation>()
            .Any(reversal => reversal.OriginalOperationId == operationId))
        {
            throw new InvalidOperationException(
                "L'operazione è stornata e non può essere modificata o eliminata da questa pagina.");
        }

        return investment;
    }

    private async Task EnsureChainValidAsync(
        InvestmentOperation? candidate,
        Guid? excludedOperationId,
        CancellationToken cancellationToken)
    {
        var chain = (await _operations.ListAsync(cancellationToken))
            .OfType<InvestmentOperation>()
            .Where(operation => excludedOperationId is null || operation.Id != excludedOperationId.Value)
            .ToList();

        if (candidate is not null)
        {
            chain.Add(candidate);
        }

        InvestmentPositionCalculator.Positions(chain, DateOnly.MaxValue);
        InvestmentCostBasisCalculator.Calculate(chain, DateOnly.MaxValue);
    }

    /// <summary>
    /// Archivia lo strumento quando non restano quote detenute dopo una vendita.
    /// </summary>
    private async Task ArchiveIfFullySoldAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var instrument = await _instruments.GetByIdAsync(instrumentId, cancellationToken);
        if (instrument is null || instrument.IsArchived)
        {
            return;
        }

        if (await HeldQuantityAsync(instrumentId, cancellationToken) > 0m)
        {
            return;
        }

        await _instruments.UpdateAsync(
            instrument.WithStatus(InstrumentStatus.Archived),
            cancellationToken);
    }

    private async Task<decimal> HeldQuantityAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var today = _currentUtcDate();
        var operations = await _operations.ListAsync(cancellationToken);

        return operations
            .OfType<InvestmentOperation>()
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.Instrument?.Id == instrumentId)
            .Where(operation => operation.OperationDate <= today)
            .Where(operation => operation.InvestmentKind is
                InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .Sum(SignedQuantity);
    }

    private static decimal SignedQuantity(InvestmentOperation operation)
    {
        var quantity = operation.Quantity?.Value ?? 0m;
        return operation.InvestmentKind == InvestmentOperationKind.Buy ? quantity : -quantity;
    }

    private static void EnsureOperationKind(InvestmentOperationKind kind)
    {
        if (!Enum.IsDefined(kind) || kind is not (
            InvestmentOperationKind.Buy or
            InvestmentOperationKind.Sell or
            InvestmentOperationKind.Dividend or
            InvestmentOperationKind.Coupon))
        {
            throw new ArgumentException(
                $"Il tipo di operazione {kind} non è supportato dalla pagina Investimenti.",
                nameof(kind));
        }
    }

    private static Quantity RequireQuantity(decimal? quantity)
    {
        if (quantity is null || quantity <= 0m)
        {
            throw new ArgumentException("La quantità deve essere positiva.", nameof(quantity));
        }

        return new Quantity(quantity.Value);
    }

    private static QuotedPrice BuildQuotedPrice(Instrument instrument, decimal? price)
    {
        if (price is null || price <= 0m)
        {
            throw new ArgumentException("Il prezzo deve essere positivo.", nameof(price));
        }

        return instrument.PriceUnit switch
        {
            PriceUnit.PerUnit => QuotedPrice.AtUnitPrice(
                new UnitPrice(new Money(price.Value, instrument.QuoteCurrency))),
            PriceUnit.PercentOfNominal => QuotedPrice.AsPercentOfNominal(price.Value),
            _ => throw new InvalidOperationException(
                $"L'unità di prezzo {instrument.PriceUnit} non è supportata."),
        };
    }

    private static IReadOnlyList<FeeComponent>? BuildFees(
        InvestmentOperationKind kind,
        decimal? feeAmount,
        Account? account)
    {
        if (kind is InvestmentOperationKind.Dividend or InvestmentOperationKind.Coupon)
        {
            return null;
        }

        if (kind is not (InvestmentOperationKind.Buy or InvestmentOperationKind.Sell))
        {
            throw new ArgumentException(
                "Le commissioni sono ammesse solo per acquisti e vendite.",
                nameof(feeAmount));
        }

        if (feeAmount is < 0m)
        {
            throw new ArgumentException(
                "Le commissioni non possono essere negative.",
                nameof(feeAmount));
        }

        if (feeAmount is not > 0m)
        {
            return null;
        }

        var currency = account?.Currency ?? Currency.Euro;
        var feeKind = kind == InvestmentOperationKind.Buy ? FeeKind.BuyFee : FeeKind.SellFee;
        return
        [
            new FeeComponent(
                feeKind,
                new PositiveMoney(new Money(feeAmount.Value, currency)),
                account),
        ];
    }

    private static PositiveMoney BuildAmount(decimal? amount, Account? account)
    {
        if (amount is null || amount <= 0m)
        {
            throw new ArgumentException("L'importo deve essere positivo.", nameof(amount));
        }

        var currency = account?.Currency ?? Currency.Euro;
        return new PositiveMoney(new Money(amount.Value, currency));
    }
}
