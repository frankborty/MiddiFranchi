using System.Globalization;
using System.IO.Compression;
using System.Text;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.DataImport;

internal sealed record ParsedAccount(
    string ExternalId,
    string Name,
    AccountType Type,
    AccountStatus Status,
    string? ColorHex,
    string? IconDataUrl,
    int Line);

internal sealed record ParsedBalance(
    string ExternalId,
    string AccountExternalId,
    DateOnly Date,
    decimal Amount,
    int Line);

internal sealed record ParsedInstrument(
    string ExternalId,
    string Name,
    AssetClass AssetClass,
    InstrumentStatus Status,
    string? Symbol,
    string? Isin,
    PriceUnit PriceUnit,
    decimal? NotionalValue,
    string? ReferenceUrl,
    string? Notes,
    DateOnly? MaturityDate,
    decimal? CouponRate,
    decimal TaxRate,
    int Line);

internal sealed record ParsedPrice(
    string ExternalId,
    string InstrumentExternalId,
    DateOnly Date,
    decimal Price,
    string? Currency,
    string Source,
    int Line);

internal sealed record ParsedOperation(
    string ExternalId,
    InvestmentOperationKind Kind,
    DateOnly Date,
    string InstrumentExternalId,
    string AccountExternalId,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    string? Description,
    int Line);

internal sealed class ParsedImport
{
    public List<ParsedAccount> Accounts { get; } = [];

    public List<ParsedBalance> Balances { get; } = [];

    public List<ParsedInstrument> Instruments { get; } = [];

    public List<ParsedPrice> Prices { get; } = [];

    public List<ParsedOperation> Operations { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Errors { get; } = [];

    public int TotalRows =>
        Accounts.Count + Balances.Count + Instruments.Count + Prices.Count + Operations.Count;
}

internal static class ImportFileParser
{
    public static ParsedImport Parse(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanSeek)
        {
            var buffer = new MemoryStream();
            source.CopyTo(buffer);
            buffer.Position = 0;
            source = buffer;
        }

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var result = new ParsedImport();

        var accountsText = ReadEntry(archive, ImportFileNames.Accounts);
        var balancesText = ReadEntry(archive, ImportFileNames.AccountBalances);
        var instrumentsText = ReadEntry(archive, ImportFileNames.Instruments);
        var pricesText = ReadEntry(archive, ImportFileNames.InstrumentPrices);
        var operationsText = ReadEntry(archive, ImportFileNames.Operations);

        var present = new[]
        {
            accountsText,
            balancesText,
            instrumentsText,
            pricesText,
            operationsText,
        }.Count(text => text is not null);

        if (present == 0)
        {
            result.Errors.Add(
                "L'archivio non contiene nessuno dei file CSV attesi: " +
                string.Join(", ", ImportFileNames.All) + ".");
            return result;
        }

        foreach (var fileName in ImportFileNames.All)
        {
            var text = fileName switch
            {
                ImportFileNames.Accounts => accountsText,
                ImportFileNames.AccountBalances => balancesText,
                ImportFileNames.Instruments => instrumentsText,
                ImportFileNames.InstrumentPrices => pricesText,
                ImportFileNames.Operations => operationsText,
                _ => null,
            };

            if (text is null)
            {
                result.Warnings.Add($"File «{fileName}» assente: la sezione viene considerata vuota.");
            }
        }

        if (accountsText is not null)
        {
            ParseAccounts(accountsText, result);
        }

        if (balancesText is not null)
        {
            ParseBalances(balancesText, result);
        }

        if (instrumentsText is not null)
        {
            ParseInstruments(instrumentsText, result);
        }

        if (pricesText is not null)
        {
            ParsePrices(pricesText, result);
        }

        if (operationsText is not null)
        {
            ParseOperations(operationsText, result);
        }

        AddDuplicateErrors(result.Errors, ImportFileNames.Accounts,
            result.Accounts.Select(account => account.ExternalId));
        AddDuplicateErrors(result.Errors, ImportFileNames.AccountBalances,
            result.Balances.Select(balance => balance.ExternalId));
        AddDuplicateErrors(result.Errors, ImportFileNames.Instruments,
            result.Instruments.Select(instrument => instrument.ExternalId));
        AddDuplicateErrors(result.Errors, ImportFileNames.InstrumentPrices,
            result.Prices.Select(price => price.ExternalId));
        AddDuplicateErrors(result.Errors, ImportFileNames.Operations,
            result.Operations.Select(operation => operation.ExternalId));

        AddISINAndSymbolDuplicateErrors(result);

        if (result.Accounts.Any(account => account.Type == AccountType.Loans))
        {
            result.Warnings.Add(
                "I saldi dei conti di tipo Loans vengono ricostruiti dai prestiti all'avvio: " +
                "i saldi importati per questi conti potrebbero essere sovrascritti.");
        }

        result.Warnings.Add(
            "L'importazione non elimina dati esistenti: inserisce o aggiorna per external_id.");

        if (result.Operations.Count > 0)
        {
            result.Warnings.Add(
                "Le operazioni vengono importate in ordine di data; acquisti e vendite devono essere coerenti.");
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add("Nessuna riga dati trovata nei file CSV.");
        }

        return result;
    }

    private static void ParseAccounts(string text, ParsedImport result)
    {
        var document = ParseDocument(text, ImportFileNames.Accounts, result);
        if (document is null)
        {
            return;
        }

        try
        {
            document.EnsureColumns(
                "external_id", "name", "type", "status", "color_hex", "icon");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.Accounts}: {exception.Message}");
            return;
        }

        foreach (var row in document.Rows)
        {
            try
            {
                var externalId = row.Required("external_id");
                var name = row.Required("name");
                var type = ParseEnum<AccountType>(row, "type");
                var status = ParseOptionalEnum(row, "status", AccountStatus.Active);
                var color = row.GetTrimmed("color_hex");
                var icon = NormalizeIcon(row.GetTrimmed("icon"));

                _ = new Account(
                    ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, externalId),
                    name,
                    type,
                    Currency.Euro,
                    status,
                    color,
                    icon);

                result.Accounts.Add(new ParsedAccount(
                    externalId, name, type, status, color, icon, row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                result.Errors.Add(
                    $"{ImportFileNames.Accounts}, riga {row.Line}: {exception.Message}");
            }
        }
    }

    private static void ParseBalances(string text, ParsedImport result)
    {
        var document = ParseDocument(text, ImportFileNames.AccountBalances, result);
        if (document is null)
        {
            return;
        }

        try
        {
            document.EnsureColumns("external_id", "account_external_id", "date", "amount");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.AccountBalances}: {exception.Message}");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var row in document.Rows)
        {
            try
            {
                var externalId = row.Required("external_id");
                var accountExternalId = row.Required("account_external_id");
                var date = ParseDate(row, "date");
                if (date > today)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.AccountBalances}, riga {row.Line}: " +
                        "la data del saldo non può essere nel futuro.");
                }

                var amount = ParseDecimal(row, "amount");
                result.Balances.Add(new ParsedBalance(
                    externalId, accountExternalId, date, amount, row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
        }
    }

    private static void ParseInstruments(string text, ParsedImport result)
    {
        var document = ParseDocument(text, ImportFileNames.Instruments, result);
        if (document is null)
        {
            return;
        }

        try
        {
            document.EnsureColumns(
                "external_id", "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.Instruments}: {exception.Message}");
            return;
        }

        foreach (var row in document.Rows)
        {
            try
            {
                var externalId = row.Required("external_id");
                var name = row.Required("name");
                var assetClass = ParseEnum<AssetClass>(row, "asset_class");
                var status = ParseOptionalEnum(row, "status", InstrumentStatus.Active);
                var symbol = row.GetTrimmed("symbol");
                var isin = row.GetTrimmed("isin");
                EnsureEuro(row, "quote_currency");
                var priceUnit = ParseEnum<PriceUnit>(row, "price_unit");
                var notionalValue = ParseOptionalDecimal(row, "notional_value");
                EnsureEuro(row, "notional_currency");
                var referenceUrl = row.GetTrimmed("reference_url");
                var notes = row.GetTrimmed("notes");
                var maturityDate = ParseOptionalDate(row, "maturity_date");
                var couponRate = ParseOptionalDecimal(row, "coupon_rate");
                var taxRate = ParseOptionalDecimal(row, "tax_rate")
                    ?? Instrument.DefaultTaxRate;

                var notional = notionalValue is { } value
                    ? new Money(value, Currency.Euro)
                    : (Money?)null;

                _ = new Instrument(
                    ExternalIdMapper.ToGuid(ExternalIdMapper.InstrumentEntity, externalId),
                    name,
                    assetClass,
                    Currency.Euro,
                    priceUnit,
                    notional,
                    symbol,
                    isin,
                    referenceUrl,
                    notes,
                    status,
                    maturityDate,
                    couponRate,
                    taxRate);

                result.Instruments.Add(new ParsedInstrument(
                    externalId,
                    name,
                    assetClass,
                    status,
                    symbol,
                    isin,
                    priceUnit,
                    notionalValue,
                    referenceUrl,
                    notes,
                    maturityDate,
                    couponRate,
                    taxRate,
                    row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                result.Errors.Add(
                    $"{ImportFileNames.Instruments}, riga {row.Line}: {exception.Message}");
            }
        }
    }

    private static void ParsePrices(string text, ParsedImport result)
    {
        var document = ParseDocument(text, ImportFileNames.InstrumentPrices, result);
        if (document is null)
        {
            return;
        }

        try
        {
            document.EnsureColumns(
                "external_id", "instrument_external_id", "date", "price", "currency", "source");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.InstrumentPrices}: {exception.Message}");
            return;
        }

        foreach (var row in document.Rows)
        {
            try
            {
                var externalId = row.Required("external_id");
                var instrumentExternalId = row.Required("instrument_external_id");
                var date = ParseDate(row, "date");
                var price = ParseDecimal(row, "price");
                if (price <= 0m)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.InstrumentPrices}, riga {row.Line}: " +
                        "il prezzo deve essere positivo.");
                }

                var currency = row.GetTrimmed("currency");
                if (!string.IsNullOrEmpty(currency) &&
                    !string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.InstrumentPrices}, riga {row.Line}: " +
                        "è supportata solo la valuta EUR.");
                }

                var source = row.GetTrimmed("source");
                if (string.IsNullOrEmpty(source))
                {
                    source = "Import";
                }

                result.Prices.Add(new ParsedPrice(
                    externalId,
                    instrumentExternalId,
                    date,
                    price,
                    string.IsNullOrEmpty(currency) ? null : "EUR",
                    source,
                    row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
        }
    }

    private static void ParseOperations(string text, ParsedImport result)
    {
        var document = ParseDocument(text, ImportFileNames.Operations, result);
        if (document is null)
        {
            return;
        }

        try
        {
            document.EnsureColumns(
                "external_id", "kind", "date", "instrument_external_id",
                "settlement_account_external_id", "quantity", "price", "fee", "description");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.Operations}: {exception.Message}");
            return;
        }

        foreach (var row in document.Rows)
        {
            try
            {
                var externalId = row.Required("external_id");
                var kind = ParseEnum<InvestmentOperationKind>(row, "kind");
                if (kind is not (InvestmentOperationKind.Buy or InvestmentOperationKind.Sell))
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.Operations}, riga {row.Line}: " +
                        "sono supportati solo Buy e Sell.");
                }

                var date = ParseDate(row, "date");
                var instrumentExternalId = row.Required("instrument_external_id");
                var accountExternalId = row.Required("settlement_account_external_id");
                var quantity = ParseDecimal(row, "quantity");
                if (quantity <= 0m)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.Operations}, riga {row.Line}: " +
                        "la quantità deve essere positiva.");
                }

                var price = ParseDecimal(row, "price");
                if (price <= 0m)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.Operations}, riga {row.Line}: " +
                        "il prezzo deve essere positivo.");
                }

                var fee = ParseOptionalDecimal(row, "fee") ?? 0m;
                if (fee < 0m)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.Operations}, riga {row.Line}: " +
                        "la commissione non può essere negativa.");
                }

                var description = row.GetTrimmed("description");

                result.Operations.Add(new ParsedOperation(
                    externalId,
                    kind,
                    date,
                    instrumentExternalId,
                    accountExternalId,
                    quantity,
                    price,
                    fee,
                    string.IsNullOrEmpty(description) ? null : description,
                    row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
        }
    }

    private static CsvDocument? ParseDocument(
        string text,
        string fileName,
        ParsedImport result)
    {
        try
        {
            return CsvDocument.Parse(text, fileName);
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add(exception.Message);
            return null;
        }
    }

    private static string? ReadEntry(ZipArchive archive, string fileName)
    {
        var entry = archive.GetEntry(fileName)
            ?? archive.Entries.FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetFileName(candidate.FullName),
                    fileName,
                    StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static T ParseEnum<T>(CsvRow row, string column)
        where T : struct, Enum
    {
        var value = row.Required(column);
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: valore non valido «{value}» per «{column}». " +
                $"Valori ammessi: {string.Join(", ", Enum.GetNames<T>())}.");
        }

        return parsed;
    }

    private static T ParseOptionalEnum<T>(CsvRow row, string column, T defaultValue)
        where T : struct, Enum
    {
        var value = row.GetTrimmed(column);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        if (!Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: valore non valido «{value}» per «{column}». " +
                $"Valori ammessi: {string.Join(", ", Enum.GetNames<T>())}.");
        }

        return parsed;
    }

    private static DateOnly ParseDate(CsvRow row, string column)
    {
        var value = row.Required(column);
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: data non valida «{value}» in «{column}». " +
                "Formato richiesto: yyyy-MM-dd.");
        }

        return date;
    }

    private static DateOnly? ParseOptionalDate(CsvRow row, string column)
    {
        var value = row.GetTrimmed(column);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: data non valida «{value}» in «{column}». " +
                "Formato richiesto: yyyy-MM-dd.");
        }

        return date;
    }

    private static decimal ParseDecimal(CsvRow row, string column)
    {
        var value = row.Required(column);
        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: numero non valido «{value}» in «{column}». " +
                "Usa il punto come separatore decimale.");
        }

        return number;
    }

    private static decimal? ParseOptionalDecimal(CsvRow row, string column)
    {
        var value = row.GetTrimmed(column);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: numero non valido «{value}» in «{column}». " +
                "Usa il punto come separatore decimale.");
        }

        return number;
    }

    private static void EnsureEuro(CsvRow row, string column)
    {
        var value = row.GetTrimmed(column);
        if (string.IsNullOrEmpty(value) ||
            string.Equals(value, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ImportValidationException(
            $"{row.FileName}, riga {row.Line}: è supportata solo la valuta EUR.");
    }

    private static string? NormalizeIcon(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("icon:", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return "icon:" + normalized;
    }

    private static void AddDuplicateErrors(
        List<string> errors,
        string fileName,
        IEnumerable<string> externalIds)
    {
        var duplicates = externalIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"{fileName}: external_id duplicato «{duplicate}».");
        }
    }

    private static void AddISINAndSymbolDuplicateErrors(ParsedImport result)
    {
        var duplicateIsins = result.Instruments
            .Where(instrument => instrument.Isin is not null)
            .GroupBy(instrument => instrument.Isin!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var isin in duplicateIsins)
        {
            result.Errors.Add(
                $"{ImportFileNames.Instruments}: ISIN duplicato «{isin}».");
        }

        var duplicateSymbols = result.Instruments
            .Where(instrument => instrument.Symbol is not null)
            .GroupBy(instrument => instrument.Symbol!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var symbol in duplicateSymbols)
        {
            result.Errors.Add(
                $"{ImportFileNames.Instruments}: simbolo duplicato «{symbol}».");
        }
    }
}
