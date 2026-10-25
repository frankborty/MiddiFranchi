using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.DataImport;

public sealed class DataImportService
{
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _balances;
    private readonly IInstrumentRepository _instruments;
    private readonly IPriceObservationRepository _prices;
    private readonly IFinancialOperationRepository _operations;
    private readonly InvestmentAssetService _assets;
    private readonly ITransactionRunner _transactions;
    private readonly DataMaintenanceGate _gate;

    public DataImportService(
        IAccountRepository accounts,
        IManualBalanceRepository balances,
        IInstrumentRepository instruments,
        IPriceObservationRepository prices,
        IFinancialOperationRepository operations,
        InvestmentAssetService assets,
        ITransactionRunner transactions,
        DataMaintenanceGate gate)
    {
        _accounts = accounts;
        _balances = balances;
        _instruments = instruments;
        _prices = prices;
        _operations = operations;
        _assets = assets;
        _transactions = transactions;
        _gate = gate;
    }

    public async Task<ImportPreview> InspectAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var parsed = ImportFileParser.Parse(source);
        await ValidateAsync(parsed, cancellationToken).ConfigureAwait(false);

        return new ImportPreview(
            parsed.Accounts.Count,
            parsed.Balances.Count,
            parsed.Instruments.Count,
            parsed.Prices.Count,
            parsed.Operations.Count,
            parsed.Warnings,
            parsed.Errors);
    }

    public async Task<ImportResult> ImportAsync(
        Stream source,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!string.Equals(
                confirmation,
                ImportContract.ConfirmationPhrase,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Per confermare l'importazione digita «{ImportContract.ConfirmationPhrase}».",
                nameof(confirmation));
        }

        var parsed = ImportFileParser.Parse(source);
        await ValidateAsync(parsed, cancellationToken).ConfigureAwait(false);

        if (parsed.Errors.Count > 0)
        {
            throw new ImportValidationException(parsed.Errors);
        }

        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await _transactions
                .RunAsync(token => ImportParsedAsync(parsed, token), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ImportValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ImportValidationException(
                $"Importazione non riuscita: {exception.Message}",
                exception);
        }
    }

    public Task<ImportTemplate> CreateTemplateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImportTemplateBuilder.Build());
    }

    private async Task ValidateAsync(
        ParsedImport parsed,
        CancellationToken cancellationToken)
    {
        var existingAccountIds = (await _accounts.ListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(account => account.Id)
            .ToHashSet();
        var existingInstrumentIds = (await _instruments.ListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(instrument => instrument.Id)
            .ToHashSet();

        var availableAccountIds = existingAccountIds
            .Union(parsed.Accounts.Select(account =>
                ExternalIdMapper.ToGuid(
                    ExternalIdMapper.AccountEntity,
                    account.ExternalId)))
            .ToHashSet();
        var availableInstrumentIds = existingInstrumentIds
            .Union(parsed.Instruments.Select(instrument =>
                ExternalIdMapper.ToGuid(
                    ExternalIdMapper.InstrumentEntity,
                    instrument.ExternalId)))
            .ToHashSet();

        foreach (var balance in parsed.Balances)
        {
            var accountId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountEntity,
                balance.AccountExternalId);
            if (!availableAccountIds.Contains(accountId))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.AccountBalances}, riga {balance.Line}: " +
                    $"il conto «{balance.AccountExternalId}» non esiste nel file né nel database.");
            }
        }

        foreach (var price in parsed.Prices)
        {
            var instrumentId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                price.InstrumentExternalId);
            if (!availableInstrumentIds.Contains(instrumentId))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.InstrumentPrices}, riga {price.Line}: " +
                    $"lo strumento «{price.InstrumentExternalId}» non esiste nel file né nel database.");
            }
        }

        foreach (var operation in parsed.Operations)
        {
            var instrumentId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                operation.InstrumentExternalId);
            var accountId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountEntity,
                operation.AccountExternalId);

            if (!availableInstrumentIds.Contains(instrumentId))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Operations}, riga {operation.Line}: " +
                    $"lo strumento «{operation.InstrumentExternalId}» non esiste nel file né nel database.");
            }

            if (!availableAccountIds.Contains(accountId))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Operations}, riga {operation.Line}: " +
                    $"il conto «{operation.AccountExternalId}» non esiste nel file né nel database.");
            }
        }

        foreach (var instrument in parsed.Instruments)
        {
            var instrumentId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                instrument.ExternalId);

            if (instrument.Isin is { } isin &&
                await _instruments.IsinExistsAsync(isin, instrumentId, cancellationToken)
                    .ConfigureAwait(false))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Instruments}, riga {instrument.Line}: " +
                    $"l'ISIN «{isin}» è già usato da un altro strumento.");
            }

            if (instrument.Symbol is { } symbol &&
                await _instruments.SymbolExistsAsync(symbol, instrumentId, cancellationToken)
                    .ConfigureAwait(false))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Instruments}, riga {instrument.Line}: " +
                    $"il simbolo «{symbol}» è già usato da un altro strumento.");
            }
        }

        var instrumentsByExternalId = parsed.Instruments
            .ToDictionary(instrument => instrument.ExternalId, StringComparer.Ordinal);

        foreach (var price in parsed.Prices)
        {
            if (!instrumentsByExternalId.TryGetValue(price.InstrumentExternalId, out var instrument))
            {
                continue;
            }

            if (instrument.PriceUnit == PriceUnit.PercentOfNominal && price.Currency is not null)
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.InstrumentPrices}, riga {price.Line}: " +
                    "per gli strumenti in percentuale del nominale la valuta deve essere vuota.");
            }
        }
    }

    private async Task<ImportResult> ImportParsedAsync(
        ParsedImport parsed,
        CancellationToken cancellationToken)
    {
        var accountsCreated = 0;
        var accountsUpdated = 0;
        var balancesCreated = 0;
        var balancesUpdated = 0;
        var instrumentsCreated = 0;
        var instrumentsUpdated = 0;
        var pricesCreated = 0;
        var pricesUpdated = 0;

        var accountById = new Dictionary<Guid, Account>();
        foreach (var parsedAccount in parsed.Accounts)
        {
            var id = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountEntity,
                parsedAccount.ExternalId);
            var account = new Account(
                id,
                parsedAccount.Name,
                parsedAccount.Type,
                Currency.Euro,
                parsedAccount.Status,
                parsedAccount.ColorHex,
                parsedAccount.IconDataUrl);

            var existing = await _accounts.GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
                accountsCreated++;
            }
            else
            {
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                accountsUpdated++;
            }

            accountById[id] = account;
        }

        foreach (var parsedBalance in parsed.Balances)
        {
            var accountId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountEntity,
                parsedBalance.AccountExternalId);
            if (!accountById.TryGetValue(accountId, out var account))
            {
                account = await _accounts.GetByIdAsync(accountId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new ImportValidationException(
                        $"Il conto «{parsedBalance.AccountExternalId}» non esiste.");
                accountById[accountId] = account;
            }

            var balanceId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountBalanceEntity,
                parsedBalance.ExternalId);
            var recordedAt = new DateTimeOffset(
                parsedBalance.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            var balance = new ManualBalance(
                balanceId,
                account,
                new Money(parsedBalance.Amount, Currency.Euro),
                parsedBalance.Date,
                recordedAt);

            var existing = await _balances.GetByIdAsync(balanceId, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                await _balances.AddAsync(balance, cancellationToken).ConfigureAwait(false);
                balancesCreated++;
            }
            else
            {
                await _balances.UpdateAsync(balance, cancellationToken).ConfigureAwait(false);
                balancesUpdated++;
            }
        }

        var instrumentById = new Dictionary<Guid, Instrument>();
        foreach (var parsedInstrument in parsed.Instruments)
        {
            var id = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                parsedInstrument.ExternalId);
            var notional = parsedInstrument.NotionalValue is { } value
                ? new Money(value, Currency.Euro)
                : (Money?)null;
            var instrument = new Instrument(
                id,
                parsedInstrument.Name,
                parsedInstrument.AssetClass,
                Currency.Euro,
                parsedInstrument.PriceUnit,
                notional,
                parsedInstrument.Symbol,
                parsedInstrument.Isin,
                parsedInstrument.ReferenceUrl,
                parsedInstrument.Notes,
                parsedInstrument.Status,
                parsedInstrument.MaturityDate,
                parsedInstrument.CouponRate);

            var existing = await _instruments.GetByIdAsync(id, cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                await _instruments.AddAsync(instrument, cancellationToken).ConfigureAwait(false);
                instrumentsCreated++;
            }
            else
            {
                await _instruments.UpdateAsync(instrument, cancellationToken)
                    .ConfigureAwait(false);
                instrumentsUpdated++;
            }

            instrumentById[id] = instrument;
        }

        var existingPriceIds = (await _prices.ListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(price => price.Id)
            .ToHashSet();

        foreach (var parsedPrice in parsed.Prices)
        {
            var instrumentId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                parsedPrice.InstrumentExternalId);
            if (!instrumentById.TryGetValue(instrumentId, out var instrument))
            {
                instrument = await _instruments.GetByIdAsync(instrumentId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new ImportValidationException(
                        $"Lo strumento «{parsedPrice.InstrumentExternalId}» non esiste.");
                instrumentById[instrumentId] = instrument;
            }

            if (instrument.PriceUnit == PriceUnit.PercentOfNominal &&
                parsedPrice.Currency is not null)
            {
                throw new ImportValidationException(
                    $"{ImportFileNames.InstrumentPrices}, riga {parsedPrice.Line}: " +
                    "per gli strumenti in percentuale del nominale la valuta deve essere vuota.");
            }

            var priceId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentPriceEntity,
                parsedPrice.ExternalId);
            var observedAt = new DateTimeOffset(
                parsedPrice.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);

            PriceObservation price = instrument.PriceUnit switch
            {
                PriceUnit.PerUnit => PriceObservation.AtUnitPrice(
                    priceId,
                    instrument,
                    new UnitPrice(new Money(parsedPrice.Price, Currency.Euro)),
                    parsedPrice.Date,
                    observedAt,
                    parsedPrice.Source),
                PriceUnit.PercentOfNominal => PriceObservation.AsPercentOfNominal(
                    priceId,
                    instrument,
                    parsedPrice.Price,
                    parsedPrice.Date,
                    observedAt,
                    parsedPrice.Source),
                _ => throw new InvalidOperationException(
                    $"L'unità di prezzo {instrument.PriceUnit} non è supportata."),
            };

            if (existingPriceIds.Contains(priceId))
            {
                await _prices.UpdateAsync(price, cancellationToken).ConfigureAwait(false);
                pricesUpdated++;
            }
            else
            {
                await _prices.AddAsync(price, cancellationToken).ConfigureAwait(false);
                existingPriceIds.Add(priceId);
                pricesCreated++;
            }
        }

        var existingOperationIds = (await _operations.ListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(operation => operation.Id)
            .ToHashSet();

        var requests = new List<ImportInvestmentOperationRequest>();
        var operationsSkipped = 0;

        foreach (var parsedOperation in parsed.Operations
                     .OrderBy(operation => operation.Date)
                     .ThenBy(operation =>
                         operation.Kind == InvestmentOperationKind.Buy ? 0 : 1)
                     .ThenBy(operation => operation.ExternalId, StringComparer.Ordinal))
        {
            var operationId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.OperationEntity,
                parsedOperation.ExternalId);
            if (existingOperationIds.Contains(operationId))
            {
                operationsSkipped++;
                continue;
            }

            var instrumentId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.InstrumentEntity,
                parsedOperation.InstrumentExternalId);
            var accountId = ExternalIdMapper.ToGuid(
                ExternalIdMapper.AccountEntity,
                parsedOperation.AccountExternalId);
            var recordedAt = new DateTimeOffset(
                parsedOperation.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);

            requests.Add(new ImportInvestmentOperationRequest(
                operationId,
                recordedAt,
                new RegisterInvestmentOperationRequest(
                    instrumentId,
                    accountId,
                    parsedOperation.Kind,
                    parsedOperation.Date,
                    Quantity: parsedOperation.Quantity,
                    Price: parsedOperation.Price,
                    FeeAmount: parsedOperation.Fee,
                    Amount: null,
                    Description: parsedOperation.Description)));
        }

        var importedOperations = await _assets
            .ImportOperationsAsync(requests, cancellationToken)
            .ConfigureAwait(false);

        return new ImportResult(
            accountsCreated,
            accountsUpdated,
            balancesCreated,
            balancesUpdated,
            instrumentsCreated,
            instrumentsUpdated,
            pricesCreated,
            pricesUpdated,
            importedOperations.Count,
            operationsSkipped);
    }
}
