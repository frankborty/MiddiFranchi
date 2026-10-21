namespace MiddiFranchi.Domain.Financial;

public static class ManualBalances
{
    /// <summary>
    /// Saldo corrente del conto: l'ultima rilevazione registrata. L'ordine di registrazione
    /// risolve anche i casi con orologi non sincronizzati: a parità di RecordedAt decide Id.
    /// Una rilevazione con data economica futura resta ammessa come corrente finché registrata.
    /// </summary>
    public static ManualBalance? Latest(IReadOnlyList<ManualBalance> readings, Guid accountId)
    {
        if (readings is null)
        {
            throw new ArgumentNullException(nameof(readings));
        }

        return readings
            .Where(reading => reading.AccountId == accountId)
            .OrderByDescending(reading => reading.RecordedAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Rilevazione valida alla data richiesta: massima ValidAt non successiva a asOfDate,
    /// risolta per (RecordedAt, Id) discendenti a parità di data.
    /// </summary>
    public static ManualBalance? LatestOnOrBefore(
        IReadOnlyList<ManualBalance> readings,
        Guid accountId,
        DateOnly asOfDate)
    {
        if (readings is null)
        {
            throw new ArgumentNullException(nameof(readings));
        }

        return readings
            .Where(reading => reading.AccountId == accountId && reading.ValidAt <= asOfDate)
            .OrderByDescending(reading => reading.ValidAt)
            .ThenByDescending(reading => reading.RecordedAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();
    }
}
