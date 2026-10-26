using System.Globalization;
using System.IO.Compression;
using System.Text;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.DataImport;

internal sealed record ParsedBalance(
    string AccountName,
    DateOnly Date,
    decimal Amount,
    int Line);

internal sealed record ParsedInstrument(
    string Name,
    AssetClass AssetClass,
    InstrumentStatus Status,
    string? Symbol,
    string Isin,
    PriceUnit PriceUnit,
    decimal? NotionalValue,
    string? ReferenceUrl,
    string? Notes,
    DateOnly? MaturityDate,
    decimal? CouponRate,
    decimal TaxRate,
    int Line);

internal sealed record ParsedPrice(
    string? InstrumentIsin,
    string? InstrumentSymbol,
    string? InstrumentName,
    DateOnly Date,
    decimal Price,
    string? Currency,
    string Source,
    int Line);

internal sealed record ParsedOperation(
    InvestmentOperationKind Kind,
    DateOnly Date,
    string? InstrumentIsin,
    string? InstrumentSymbol,
    string? InstrumentName,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    string? Description,
    int Line);

internal sealed class ParsedImport
{
    public List<AccountsImportRow> Accounts { get; } = [];

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

        AddBalanceDuplicateErrors(result);
        AddISINAndSymbolDuplicateErrors(result);

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
        var file = AccountsImportParser.ReadText(text);
        result.Accounts.AddRange(file.Rows);
        result.Errors.AddRange(file.Errors);
        result.Warnings.AddRange(file.Warnings);
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
            document.EnsureColumns("account_name", "date", "amount");
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
                var accountName = row.Required("account_name");
                var date = ParseDate(row, "date");
                if (date > today)
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.AccountBalances}, riga {row.Line}: " +
                        "la data del saldo non può essere nel futuro.");
                }

                var amount = ParseDecimal(row, "amount");
                result.Balances.Add(new ParsedBalance(
                    accountName,
                    date,
                    amount,
                    row.Line));
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
            document.EnsureColumns("name", "asset_class", "price_unit", "isin");
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
                var isin = row.Required("isin");
                var name = row.Required("name");
                var assetClass = ParseEnum<AssetClass>(row, "asset_class");
                var status = ParseOptionalEnum(row, "status", InstrumentStatus.Active);
                var symbol = NullIfEmpty(row.GetTrimmed("symbol"));
                EnsureEuro(row, "quote_currency");
                var priceUnit = ParseEnum<PriceUnit>(row, "price_unit");
                var notionalValue = ParseOptionalDecimal(row, "notional_value");
                EnsureEuro(row, "notional_currency");
                var referenceUrl = NullIfEmpty(row.GetTrimmed("reference_url"));
                var notes = NullIfEmpty(row.GetTrimmed("notes"));
                var maturityDate = ParseOptionalDate(row, "maturity_date");
                var couponRate = ParseOptionalDecimal(row, "coupon_rate");
                var taxRate = ParseOptionalDecimal(row, "tax_rate")
                    ?? Instrument.DefaultTaxRate;

                var notional = notionalValue is { } value
                    ? new Money(value, Currency.Euro)
                    : (Money?)null;

                var instrumentId = ImportIdMapper.ToInstrumentGuidByIsin(isin);

                _ = new Instrument(
                    instrumentId,
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
            document.EnsureColumns("date", "price");
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
                var (instrumentIsin, instrumentSymbol, instrumentName) =
                    ParseInstrumentReference(row, ImportFileNames.InstrumentPrices);
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
                    instrumentIsin,
                    instrumentSymbol,
                    instrumentName,
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
            document.EnsureColumns("kind", "date", "quantity", "price");
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
                var kind = ParseEnum<InvestmentOperationKind>(row, "kind");
                if (kind is not (InvestmentOperationKind.Buy or InvestmentOperationKind.Sell))
                {
                    throw new ImportValidationException(
                        $"{ImportFileNames.Operations}, riga {row.Line}: " +
                        "sono supportati solo Buy e Sell.");
                }

                var date = ParseDate(row, "date");
                var (instrumentIsin, instrumentSymbol, instrumentName) =
                    ParseInstrumentReference(row, ImportFileNames.Operations);

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
                    kind,
                    date,
                    instrumentIsin,
                    instrumentSymbol,
                    instrumentName,
                    quantity,
                    price,
                    fee,
                    NullIfEmpty(description),
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

    internal static T ParseEnum<T>(CsvRow row, string column)
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

    internal static T ParseOptionalEnum<T>(CsvRow row, string column, T defaultValue)
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

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static (string? Isin, string? Symbol, string? Name) ParseInstrumentReference(
        CsvRow row,
        string fileName)
    {
        var isin = NullIfEmpty(row.GetTrimmed("instrument_isin"));
        var symbol = NullIfEmpty(row.GetTrimmed("instrument_symbol"));
        var name = NullIfEmpty(row.GetTrimmed("instrument_name"));
        if (isin is null && symbol is null && name is null)
        {
            throw new ImportValidationException(
                $"{fileName}, riga {row.Line}: indica almeno uno tra instrument_isin, " +
                "instrument_symbol e instrument_name.");
        }

        return (isin, symbol, name);
    }

    internal static string? NormalizeIcon(string? value)
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

    private static void AddBalanceDuplicateErrors(ParsedImport result)
    {
        var duplicatedKeys = result.Balances
            .GroupBy(
                balance => $"{balance.AccountName.Trim()}|{balance.Date}",
                StringComparer.CurrentCultureIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .ToArray();

        foreach (var balance in duplicatedKeys)
        {
            result.Errors.Add(
                $"{ImportFileNames.AccountBalances}: saldo duplicato per il conto " +
                $"«{balance.AccountName}» alla data {balance.Date:yyyy-MM-dd}.");
        }
    }

    private static void AddISINAndSymbolDuplicateErrors(ParsedImport result)
    {
        var duplicateIsins = result.Instruments
            .GroupBy(instrument => instrument.Isin, StringComparer.OrdinalIgnoreCase)
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
