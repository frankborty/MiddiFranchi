namespace MiddiFranchi.Application.DataImport;

public static class ImportFileNames
{
    public const string Accounts = "accounts.csv";
    public const string AccountBalances = "account_balances.csv";
    public const string Instruments = "instruments.csv";
    public const string InstrumentPrices = "instrument_prices.csv";
    public const string Operations = "operations.csv";
    public const string Readme = "README.txt";

    public static IReadOnlyList<string> All { get; } =
    [
        Accounts,
        AccountBalances,
        Instruments,
        InstrumentPrices,
        Operations,
    ];
}

public static class ImportContract
{
    public const string ConfirmationPhrase = "IMPORTA";
}

public sealed record ImportPreview(
    int Accounts,
    int AccountBalances,
    int Instruments,
    int InstrumentPrices,
    int Operations,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;

    public int TotalRows =>
        Accounts + AccountBalances + Instruments + InstrumentPrices + Operations;
}

public sealed record ImportResult(
    int AccountsCreated,
    int AccountsUpdated,
    int BalancesCreated,
    int BalancesUpdated,
    int InstrumentsCreated,
    int InstrumentsUpdated,
    int PricesCreated,
    int PricesUpdated,
    int OperationsImported,
    int OperationsSkipped)
{
    public int TotalChanged =>
        AccountsCreated + AccountsUpdated +
        BalancesCreated + BalancesUpdated +
        InstrumentsCreated + InstrumentsUpdated +
        PricesCreated + PricesUpdated +
        OperationsImported;
}

public sealed record ImportTemplate(string FileName, byte[] Content);

public sealed class ImportValidationException : Exception
{
    public ImportValidationException(string message)
        : base(message)
    {
        Errors = [message];
    }

    public ImportValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    public ImportValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
