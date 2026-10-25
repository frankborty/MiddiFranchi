namespace MiddiFranchi.Application.DataManagement;

public sealed record DataCounts(
    int Accounts,
    int Categories,
    int Instruments,
    int FinancialOperations,
    int AccountPostings,
    int FeeComponents,
    int PriceObservations,
    int ManualBalances,
    int Loans,
    int LoanReadings,
    int AppMetadata)
{
    public static DataCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public int Total =>
        Accounts + Categories + Instruments + FinancialOperations + AccountPostings +
        FeeComponents + PriceObservations + ManualBalances + Loans + LoanReadings + AppMetadata;

    public IReadOnlyDictionary<string, int> ToDictionary() => new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["accounts"] = Accounts,
        ["categories"] = Categories,
        ["instruments"] = Instruments,
        ["financialOperations"] = FinancialOperations,
        ["accountPostings"] = AccountPostings,
        ["feeComponents"] = FeeComponents,
        ["priceObservations"] = PriceObservations,
        ["manualBalances"] = ManualBalances,
        ["loans"] = Loans,
        ["loanReadings"] = LoanReadings,
        ["appMetadata"] = AppMetadata,
    };

    public static DataCounts FromDictionary(IReadOnlyDictionary<string, int> values) => new(
        Accounts: Get(values, "accounts"),
        Categories: Get(values, "categories"),
        Instruments: Get(values, "instruments"),
        FinancialOperations: Get(values, "financialOperations"),
        AccountPostings: Get(values, "accountPostings"),
        FeeComponents: Get(values, "feeComponents"),
        PriceObservations: Get(values, "priceObservations"),
        ManualBalances: Get(values, "manualBalances"),
        Loans: Get(values, "loans"),
        LoanReadings: Get(values, "loanReadings"),
        AppMetadata: Get(values, "appMetadata"));

    private static int Get(IReadOnlyDictionary<string, int> values, string key) =>
        values.TryGetValue(key, out var value) ? value : 0;
}
