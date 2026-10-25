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
    private readonly Func<DateOnly> _currentUtcDate;

    public InvestmentAssetService(
        IInstrumentRepository instruments,
        IFinancialOperationRepository operations,
        IPriceObservationRepository prices,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _instruments = instruments;
        _operations = operations;
        _prices = prices;
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
            request.Description,
            request.TaxAmount);

        await EnsureChainValidAsync(
            operation,
            excludedOperationId: null,
            cancellationToken);
        await _operations.AddAsync(operation, cancellationToken);
        return operation;
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
                item.Operation.Description,
                item.Operation.TaxAmount));
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

        return operations;
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
            request.Description,
            request.TaxAmount);

        await EnsureChainValidAsync(
            updated,
            excludedOperationId: existing.Id,
            cancellationToken);
        await _operations.UpdateInvestmentAsync(updated, cancellationToken);
        return updated;
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
        await _operations.DeleteAsync(operationId, cancellationToken);
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
        string? description,
        decimal? taxAmount)
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
                BuildFees(kind, feeAmount, taxAmount, settlementAccount),
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
                BuildFees(kind, feeAmount, taxAmount, settlementAccount),
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
                BuildFees(kind, feeAmount, taxAmount, settlementAccount)),
            InvestmentOperationKind.Coupon => InvestmentOperation.Coupon(
                id,
                instrument,
                settlementAccount,
                BuildAmount(amount, settlementAccount),
                operationDate,
                recordedAt,
                cleanDescription,
                null,
                BuildFees(kind, feeAmount, taxAmount, settlementAccount)),
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
        decimal? taxAmount,
        Account? account)
    {
        if (kind is not (
            InvestmentOperationKind.Buy or
            InvestmentOperationKind.Sell or
            InvestmentOperationKind.Dividend or
            InvestmentOperationKind.Coupon))
        {
            throw new ArgumentException(
                "Le commissioni sono ammesse solo per acquisti, vendite e cedole.",
                nameof(feeAmount));
        }

        if (feeAmount is < 0m || taxAmount is < 0m)
        {
            throw new ArgumentException(
                "Commissioni e tasse non possono essere negative.",
                nameof(feeAmount));
        }

        var currency = account?.Currency ?? Currency.Euro;
        var fees = new List<FeeComponent>();

        if (kind == InvestmentOperationKind.Buy && feeAmount is > 0m)
        {
            fees.Add(new FeeComponent(
                FeeKind.BuyFee,
                new PositiveMoney(new Money(feeAmount.Value, currency)),
                account));
        }

        if (kind == InvestmentOperationKind.Sell)
        {
            if (feeAmount is > 0m)
            {
                fees.Add(new FeeComponent(
                    FeeKind.SellFee,
                    new PositiveMoney(new Money(feeAmount.Value, currency)),
                    account));
            }

            if (taxAmount is > 0m)
            {
                fees.Add(new FeeComponent(
                    FeeKind.TaxWithheld,
                    new PositiveMoney(new Money(taxAmount.Value, currency)),
                    account));
            }
        }

        if (kind is InvestmentOperationKind.Dividend or InvestmentOperationKind.Coupon &&
            feeAmount is > 0m)
        {
            fees.Add(new FeeComponent(
                FeeKind.TaxWithheld,
                new PositiveMoney(new Money(feeAmount.Value, currency)),
                account));
        }

        return fees.Count == 0 ? null : fees;
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
