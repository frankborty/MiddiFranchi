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
    private readonly ILoanRepository _loans;
    private readonly InvestmentAssetService _assets;
    private readonly ITransactionRunner _transactions;
    private readonly DataMaintenanceGate _gate;

    public DataImportService(
        IAccountRepository accounts,
        IManualBalanceRepository balances,
        IInstrumentRepository instruments,
        IPriceObservationRepository prices,
        IFinancialOperationRepository operations,
        ILoanRepository loans,
        InvestmentAssetService assets,
        ITransactionRunner transactions,
        DataMaintenanceGate gate)
    {
        _accounts = accounts;
        _balances = balances;
        _instruments = instruments;
        _prices = prices;
        _operations = operations;
        _loans = loans;
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
        var existingAccountNames = new Dictionary<string, Account>(
            StringComparer.CurrentCultureIgnoreCase);
        foreach (var account in await _accounts.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            existingAccountNames.TryAdd(account.Name.Trim(), account);
        }

        var availableAccountNames = existingAccountNames.Keys
            .Union(parsed.Accounts.Select(account => account.Name), StringComparer.CurrentCultureIgnoreCase)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var existingInstruments = await _instruments.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingInstrumentsById = existingInstruments
            .ToDictionary(instrument => instrument.Id);
        var existingInstrumentsByIsin = existingInstruments
            .Where(instrument => instrument.Isin is not null)
            .GroupBy(instrument => instrument.Isin!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var parsedInstrumentsById = new Dictionary<Guid, ParsedInstrument>();
        foreach (var instrument in parsed.Instruments)
        {
            var instrumentId = ResolveInstrumentId(instrument, existingInstrumentsByIsin);
            parsedInstrumentsById[instrumentId] = instrument;
        }

        var instrumentLookup = new InstrumentLookup();
        foreach (var existing in existingInstruments)
        {
            instrumentLookup.Add(existing);
        }

        foreach (var (instrumentId, instrument) in parsedInstrumentsById)
        {
            instrumentLookup.Add(instrumentId, instrument.Isin, instrument.Symbol, instrument.Name);
        }

        foreach (var balance in parsed.Balances)
        {
            if (!availableAccountNames.Contains(balance.AccountName))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.AccountBalances}, riga {balance.Line}: " +
                    $"il conto «{balance.AccountName}» non esiste nel file né nel database.");
            }
        }

        var priceLinesByKey = new Dictionary<(Guid InstrumentId, DateOnly Date), int>();
        foreach (var price in parsed.Prices)
        {
            if (!TryResolveInstrument(
                    instrumentLookup,
                    price.InstrumentIsin,
                    price.InstrumentSymbol,
                    price.InstrumentName,
                    out var instrumentId,
                    out var error))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.InstrumentPrices}, riga {price.Line}: {error}");
                continue;
            }

            if (priceLinesByKey.TryAdd((instrumentId, price.Date), price.Line))
            {
                continue;
            }

            parsed.Errors.Add(
                $"{ImportFileNames.InstrumentPrices}, riga {price.Line}: " +
                $"esiste già una quotazione per lo stesso strumento alla data " +
                $"{price.Date:yyyy-MM-dd} (riga {priceLinesByKey[(instrumentId, price.Date)]}).");
        }

        foreach (var operation in parsed.Operations)
        {
            if (!TryResolveInstrument(
                    instrumentLookup,
                    operation.InstrumentIsin,
                    operation.InstrumentSymbol,
                    operation.InstrumentName,
                    out _,
                    out var error))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Operations}, riga {operation.Line}: {error}");
            }
        }

        foreach (var row in parsed.Accounts)
        {
            if (!existingAccountNames.TryGetValue(row.Name, out var existing) ||
                (existing.Type != AccountType.Loans || row.Type == AccountType.Loans))
            {
                continue;
            }

            var loans = await _loans
                .ListForAccountAsync(existing.Id, cancellationToken)
                .ConfigureAwait(false);
            if (loans.Count > 0)
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Accounts}, riga {row.Line}: " +
                    $"il conto «{existing.Name}» ha prestiti registrati: " +
                    "non può essere convertito a un altro tipo.");
            }
        }

        foreach (var instrument in parsed.Instruments)
        {
            var instrumentId = ResolveInstrumentId(instrument, existingInstrumentsByIsin);

            if (await _instruments.IsinExistsAsync(
                    instrument.Isin,
                    instrumentId,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.Instruments}, riga {instrument.Line}: " +
                    $"l'ISIN «{instrument.Isin}» è già usato da un altro strumento.");
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

        foreach (var price in parsed.Prices)
        {
            if (!TryResolveInstrument(
                    instrumentLookup,
                    price.InstrumentIsin,
                    price.InstrumentSymbol,
                    price.InstrumentName,
                    out var instrumentId,
                    out _))
            {
                continue;
            }

            var priceUnit = parsedInstrumentsById.TryGetValue(instrumentId, out var parsedInstrument)
                ? parsedInstrument.PriceUnit
                : existingInstrumentsById.TryGetValue(instrumentId, out var existingInstrument)
                    ? existingInstrument.PriceUnit
                    : (PriceUnit?)null;

            if (priceUnit == PriceUnit.PercentOfNominal && price.Currency is not null)
            {
                parsed.Errors.Add(
                    $"{ImportFileNames.InstrumentPrices}, riga {price.Line}: " +
                    "per gli strumenti in percentuale del nominale la valuta deve essere vuota.");
            }
        }
    }

    private static Guid ResolveInstrumentId(
        ParsedInstrument instrument,
        IReadOnlyDictionary<string, Instrument> existingByIsin)
    {
        // Se lo strumento esiste già con questo ISIN si aggiorna quello.
        if (existingByIsin.TryGetValue(instrument.Isin, out var existing))
        {
            return existing.Id;
        }

        return ImportIdMapper.ToInstrumentGuidByIsin(instrument.Isin);
    }

    private static bool TryResolveInstrument(
        InstrumentLookup lookup,
        string? isin,
        string? symbol,
        string? name,
        out Guid instrumentId,
        out string error)
    {
        if (isin is not null && lookup.TryGetByIsin(isin, out instrumentId))
        {
            error = "";
            return true;
        }

        if (symbol is not null && lookup.TryGetBySymbol(symbol, out instrumentId))
        {
            error = "";
            return true;
        }

        if (name is not null)
        {
            var matches = lookup.GetByName(name);
            if (matches.Count == 1)
            {
                instrumentId = matches[0];
                error = "";
                return true;
            }

            if (matches.Count > 1)
            {
                instrumentId = default;
                error = $"il nome «{name}» corrisponde a più strumenti: indica l'ISIN o il simbolo.";
                return false;
            }
        }

        instrumentId = default;
        error =
            $"lo strumento con {DescribeInstrumentReference(isin, symbol, name)} " +
            "non esiste nel file né nel database.";
        return false;
    }

    private static Guid ResolveInstrumentIdForImport(
        InstrumentLookup lookup,
        string? isin,
        string? symbol,
        string? name,
        string fileName,
        int line)
    {
        if (TryResolveInstrument(lookup, isin, symbol, name, out var instrumentId, out var error))
        {
            return instrumentId;
        }

        throw new ImportValidationException($"{fileName}, riga {line}: {error}");
    }

    private static string DescribeInstrumentReference(string? isin, string? symbol, string? name)
    {
        var parts = new List<string>(3);
        if (isin is not null)
        {
            parts.Add($"ISIN «{isin}»");
        }

        if (symbol is not null)
        {
            parts.Add($"simbolo «{symbol}»");
        }

        if (name is not null)
        {
            parts.Add($"nome «{name}»");
        }

        return string.Join(", ", parts);
    }

    private sealed class InstrumentLookup
    {
        private readonly Dictionary<string, Guid> _byIsin =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Guid> _bySymbol =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<Guid>> _byName =
            new(StringComparer.OrdinalIgnoreCase);

        public void Add(Instrument instrument) =>
            Add(instrument.Id, instrument.Isin, instrument.Symbol, instrument.Name);

        public void Add(Guid instrumentId, string? isin, string? symbol, string name)
        {
            if (isin is not null)
            {
                _byIsin[isin] = instrumentId;
            }

            if (symbol is not null)
            {
                _bySymbol[symbol] = instrumentId;
            }

            var key = name.Trim();
            if (!_byName.TryGetValue(key, out var ids))
            {
                ids = [];
                _byName[key] = ids;
            }

            if (!ids.Contains(instrumentId))
            {
                ids.Add(instrumentId);
            }
        }

        public bool TryGetByIsin(string isin, out Guid instrumentId) =>
            _byIsin.TryGetValue(isin, out instrumentId);

        public bool TryGetBySymbol(string symbol, out Guid instrumentId) =>
            _bySymbol.TryGetValue(symbol, out instrumentId);

        public IReadOnlyList<Guid> GetByName(string name) =>
            _byName.TryGetValue(name.Trim(), out var ids) ? ids : [];
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
        var loansCreated = 0;

        var existingAccountsByName = new Dictionary<string, Account>(
            StringComparer.CurrentCultureIgnoreCase);
        foreach (var existingAccount in await _accounts.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            existingAccountsByName.TryAdd(existingAccount.Name.Trim(), existingAccount);
        }

        var accountByName = new Dictionary<string, Account>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var parsedAccount in parsed.Accounts)
        {
            var name = parsedAccount.Name.Trim();
            Account account;
            if (existingAccountsByName.TryGetValue(name, out var existing))
            {
                account = new Account(
                    existing.Id,
                    existing.Name,
                    parsedAccount.Type,
                    existing.Currency,
                    parsedAccount.Status,
                    parsedAccount.ColorHex,
                    parsedAccount.IconDataUrl);
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                accountsUpdated++;
            }
            else
            {
                account = new Account(
                    ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, name),
                    name,
                    parsedAccount.Type,
                    Currency.Euro,
                    parsedAccount.Status,
                    parsedAccount.ColorHex,
                    parsedAccount.IconDataUrl);
                await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
                accountsCreated++;
                existingAccountsByName[name] = account;
            }

            accountByName[name] = account;
            if (parsedAccount.Loans.Count == 0)
            {
                continue;
            }

            var existingBorrowers = (await _loans
                    .ListForAccountAsync(account.Id, cancellationToken)
                    .ConfigureAwait(false))
                .Select(loan => loan.Borrower.Trim())
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

            foreach (var borrower in parsedAccount.Loans)
            {
                if (!existingBorrowers.Add(borrower))
                {
                    continue;
                }

                await _loans
                    .AddAsync(
                        new Loan(Guid.NewGuid(), account, borrower, DateTimeOffset.UtcNow),
                        cancellationToken)
                    .ConfigureAwait(false);
                loansCreated++;
            }
        }

        var balanceLookups = new Dictionary<Guid, Dictionary<DateOnly, ManualBalance>>();

        async Task<Dictionary<DateOnly, ManualBalance>> BalanceLookupAsync(Account account)
        {
            if (balanceLookups.TryGetValue(account.Id, out var cached))
            {
                return cached;
            }

            var existingBalances = await _balances
                .ListForAccountAsync(account.Id, cancellationToken)
                .ConfigureAwait(false);
            var lookup = existingBalances
                .GroupBy(balance => balance.ValidAt)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(balance => balance.RecordedAt)
                        .ThenByDescending(balance => balance.Id)
                        .First());
            balanceLookups[account.Id] = lookup;
            return lookup;
        }

        foreach (var parsedBalance in parsed.Balances)
        {
            if (!accountByName.TryGetValue(parsedBalance.AccountName, out var account) &&
                !existingAccountsByName.TryGetValue(
                    parsedBalance.AccountName, out account))
            {
                throw new ImportValidationException(
                    $"Il conto «{parsedBalance.AccountName}» non esiste.");
            }

            var recordedAt = new DateTimeOffset(
                parsedBalance.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);
            var amount = new Money(parsedBalance.Amount, Currency.Euro);
            var balancesByDate = await BalanceLookupAsync(account);

            ManualBalance balance;
            if (balancesByDate.TryGetValue(parsedBalance.Date, out var existingBalance))
            {
                // Il saldo è identificato da conto e data: aggiorna l'eventuale
                // rilevazione già presente invece di duplicarla.
                balance = new ManualBalance(
                    existingBalance.Id,
                    account,
                    amount,
                    parsedBalance.Date,
                    existingBalance.RecordedAt);
                await _balances.UpdateAsync(balance, cancellationToken).ConfigureAwait(false);
                balancesUpdated++;
            }
            else
            {
                var balanceId = ImportIdMapper.ToAccountBalanceGuid(
                    account.Name,
                    parsedBalance.Date);
                balance = new ManualBalance(
                    balanceId, account, amount, parsedBalance.Date, recordedAt);
                await _balances.AddAsync(balance, cancellationToken).ConfigureAwait(false);
                balancesCreated++;
            }

            balancesByDate[parsedBalance.Date] = balance;
        }

        var existingInstruments = await _instruments.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingInstrumentsById = existingInstruments
            .ToDictionary(instrument => instrument.Id);
        var existingInstrumentsByIsin = existingInstruments
            .Where(instrument => instrument.Isin is not null)
            .GroupBy(instrument => instrument.Isin!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var instrumentById = new Dictionary<Guid, Instrument>();
        foreach (var parsedInstrument in parsed.Instruments)
        {
            var id = ResolveInstrumentId(parsedInstrument, existingInstrumentsByIsin);
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
                parsedInstrument.CouponRate,
                parsedInstrument.TaxRate);

            if (existingInstrumentsById.TryGetValue(id, out var existing))
            {
                await _instruments.UpdateAsync(instrument, cancellationToken)
                    .ConfigureAwait(false);
                instrumentsUpdated++;
            }
            else
            {
                await _instruments.AddAsync(instrument, cancellationToken).ConfigureAwait(false);
                instrumentsCreated++;
            }

            existingInstrumentsById[id] = instrument;
            instrumentById[id] = instrument;
        }

        var instrumentLookup = new InstrumentLookup();
        foreach (var instrument in existingInstrumentsById.Values)
        {
            instrumentLookup.Add(instrument);
        }

        var existingPrices = await _prices.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingPriceIdByKey = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var price in existingPrices)
        {
            existingPriceIdByKey.TryAdd(
                ImportIdMapper.InstrumentPriceKey(price.Instrument.Id, price.ValidAt),
                price.Id);
        }

        foreach (var parsedPrice in parsed.Prices)
        {
            var instrumentId = ResolveInstrumentIdForImport(
                instrumentLookup,
                parsedPrice.InstrumentIsin,
                parsedPrice.InstrumentSymbol,
                parsedPrice.InstrumentName,
                ImportFileNames.InstrumentPrices,
                parsedPrice.Line);
            if (!instrumentById.TryGetValue(instrumentId, out var instrument))
            {
                if (!existingInstrumentsById.TryGetValue(instrumentId, out instrument))
                {
                    instrument = await _instruments.GetByIdAsync(instrumentId, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new ImportValidationException(
                            $"{ImportFileNames.InstrumentPrices}, riga {parsedPrice.Line}: " +
                            $"lo strumento con {DescribeInstrumentReference(
                                parsedPrice.InstrumentIsin,
                                parsedPrice.InstrumentSymbol,
                                parsedPrice.InstrumentName)} non esiste.");
                    existingInstrumentsById[instrumentId] = instrument;
                    instrumentLookup.Add(instrument);
                }

                instrumentById[instrumentId] = instrument;
            }

            if (instrument.PriceUnit == PriceUnit.PercentOfNominal &&
                parsedPrice.Currency is not null)
            {
                throw new ImportValidationException(
                    $"{ImportFileNames.InstrumentPrices}, riga {parsedPrice.Line}: " +
                    "per gli strumenti in percentuale del nominale la valuta deve essere vuota.");
            }

            var priceKey = ImportIdMapper.InstrumentPriceKey(instrumentId, parsedPrice.Date);
            var alreadyExists = existingPriceIdByKey.TryGetValue(priceKey, out var priceId);
            if (!alreadyExists)
            {
                priceId = ImportIdMapper.ToInstrumentPriceGuid(instrumentId, parsedPrice.Date);
            }

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

            if (alreadyExists)
            {
                await _prices.UpdateAsync(price, cancellationToken).ConfigureAwait(false);
                pricesUpdated++;
            }
            else
            {
                await _prices.AddAsync(price, cancellationToken).ConfigureAwait(false);
                existingPriceIdByKey[priceKey] = price.Id;
                pricesCreated++;
            }
        }

        var existingOperations = await _operations.ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingOperationKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existingOperation in existingOperations.OfType<InvestmentOperation>())
        {
            if (existingOperation.Instrument is null ||
                existingOperation.Status != OperationStatus.Registered)
            {
                continue;
            }

            existingOperationKeys.Add(ImportIdMapper.OperationKey(
                existingOperation.Instrument.Id,
                existingOperation.InvestmentKind,
                existingOperation.OperationDate,
                existingOperation.Quantity?.Value ?? 0m,
                existingOperation.QuotedPrice?.PricePerUnit?.Price.Amount
                    ?? existingOperation.QuotedPrice?.PercentOfNominal
                    ?? 0m,
                existingOperation.Fees.Sum(fee => fee.Amount.Value.Amount)));
        }

        var requests = new List<ImportInvestmentOperationRequest>();
        var operationsSkipped = 0;
        var referencedInstrumentIds = new HashSet<Guid>();

        foreach (var parsedOperation in parsed.Operations
                     .OrderBy(operation => operation.Date)
                     .ThenBy(operation =>
                         operation.Kind == InvestmentOperationKind.Buy ? 0 : 1)
                     .ThenBy(operation => operation.InstrumentIsin, StringComparer.Ordinal)
                     .ThenBy(operation => operation.InstrumentSymbol, StringComparer.Ordinal)
                     .ThenBy(operation => operation.InstrumentName, StringComparer.Ordinal))
        {
            var instrumentId = ResolveInstrumentIdForImport(
                instrumentLookup,
                parsedOperation.InstrumentIsin,
                parsedOperation.InstrumentSymbol,
                parsedOperation.InstrumentName,
                ImportFileNames.Operations,
                parsedOperation.Line);
            referencedInstrumentIds.Add(instrumentId);
            var operationKey = ImportIdMapper.OperationKey(
                instrumentId,
                parsedOperation.Kind,
                parsedOperation.Date,
                parsedOperation.Quantity,
                parsedOperation.Price,
                parsedOperation.Fee);
            if (!existingOperationKeys.Add(operationKey))
            {
                operationsSkipped++;
                continue;
            }

            var operationId = ImportIdMapper.ToOperationGuid(
                instrumentId,
                parsedOperation.Kind,
                parsedOperation.Date,
                parsedOperation.Quantity,
                parsedOperation.Price,
                parsedOperation.Fee);
            var recordedAt = new DateTimeOffset(
                parsedOperation.Date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero);

            requests.Add(new ImportInvestmentOperationRequest(
                operationId,
                recordedAt,
                new RegisterInvestmentOperationRequest(
                    instrumentId,
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

        // Le operazioni già presenti vengono saltate, ma lo stato degli strumenti va
        // comunque riallineato: uno strumento venduto del tutto deve risultare archiviato.
        await _assets
            .ArchiveSoldOutAsync(referencedInstrumentIds, cancellationToken)
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
            loansCreated,
            importedOperations.Count,
            operationsSkipped);
    }
}
