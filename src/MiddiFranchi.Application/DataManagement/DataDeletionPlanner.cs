namespace MiddiFranchi.Application.DataManagement;

public sealed record DataDeletionPlan(
    DataDataset Requested,
    DataDataset Effective,
    IReadOnlyList<string> Warnings,
    DataCounts Counts)
{
    public bool IsEmpty => Effective == DataDataset.None;
}

public static class DataDeletionPlanner
{
    public static DataDeletionPlan Build(DataDataset requested, DataCounts counts)
    {
        var effective = requested & DataDataset.Everything;
        if (effective == DataDataset.None)
        {
            return new DataDeletionPlan(DataDataset.None, DataDataset.None, [], counts);
        }

        var warnings = new List<string>();
        var dependsOnMovements =
            effective.Includes(DataDataset.Instruments) ||
            effective.Includes(DataDataset.Accounts) ||
            effective.Includes(DataDataset.Categories);

        if (dependsOnMovements && !effective.Includes(DataDataset.Movements))
        {
            effective |= DataDataset.Movements;
            warnings.Add(BuildMovementsWarning(requested, counts.FinancialOperations));
        }

        return new DataDeletionPlan(requested, effective, warnings, counts);
    }

    public static IReadOnlyList<DataDataset> DatasetsOf(DataDataset selection)
    {
        var datasets = new List<DataDataset>(4);
        foreach (var dataset in new[]
                 {
                     DataDataset.Movements,
                     DataDataset.Instruments,
                     DataDataset.Accounts,
                     DataDataset.Categories,
                 })
        {
            if (selection.Includes(dataset))
            {
                datasets.Add(dataset);
            }
        }

        return datasets;
    }

    private static string BuildMovementsWarning(DataDataset requested, int operationCount)
    {
        var count = operationCount == 1 ? "1 operazione" : $"{operationCount} operazioni";

        if (requested == DataDataset.Accounts)
        {
            return $"Per eliminare i conti verranno eliminati anche tutti i movimenti collegati ({count}).";
        }

        if (requested == DataDataset.Instruments)
        {
            return $"Per eliminare gli strumenti verranno eliminati anche tutti i movimenti collegati ({count}).";
        }

        if (requested == DataDataset.Categories)
        {
            return $"Per eliminare le categorie verranno eliminati anche tutti i movimenti collegati ({count}).";
        }

        return $"Per eliminare i dati selezionati verranno eliminati anche tutti i movimenti collegati ({count}).";
    }
}
