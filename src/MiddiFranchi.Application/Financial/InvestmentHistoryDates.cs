namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Sceglie le date da valorizzare nella cronologia degli investimenti. Quando le quotazioni
/// giornaliere sono molte, mantiene comunque l'ultima quotazione di ogni mese e i giorni recenti,
/// così i grafici mensili non perdono mesi interi.
/// </summary>
internal static class InvestmentHistoryDates
{
    private const int MaximumPointsBeforeSampling = 500;
    private const int RecentDays = 180;

    public static IReadOnlyList<DateOnly> Build(
        IEnumerable<DateOnly> operationDates,
        IEnumerable<DateOnly> observationDates,
        DateOnly valuationDate)
    {
        var operations = operationDates
            .Where(date => date <= valuationDate)
            .ToHashSet();
        if (operations.Count == 0)
        {
            return [];
        }

        var from = operations.Min();
        var observations = observationDates
            .Where(date => date >= from && date <= valuationDate)
            .ToHashSet();

        var allDates = operations
            .Concat(observations)
            .Append(valuationDate)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        if (allDates.Length <= MaximumPointsBeforeSampling)
        {
            return allDates;
        }

        var recentFrom = valuationDate.AddDays(-RecentDays);
        var lastObservationByMonth = observations
            .GroupBy(date => new DateOnly(date.Year, date.Month, 1))
            .Select(group => group.Max())
            .ToHashSet();

        return allDates
            .Where(date =>
                operations.Contains(date) ||
                date == valuationDate ||
                date >= recentFrom ||
                lastObservationByMonth.Contains(date))
            .ToArray();
    }
}
