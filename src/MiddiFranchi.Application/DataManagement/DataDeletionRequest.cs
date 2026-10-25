namespace MiddiFranchi.Application.DataManagement;

public sealed record DataDeletionRequest(DataDataset Datasets, string Confirmation)
{
    public const string ConfirmationPhrase = "ELIMINA";
}

public sealed record DataDeletionResult(
    DataDeletionPlan Plan,
    DateTimeOffset DeletedAt,
    bool PriceStatusReset);
