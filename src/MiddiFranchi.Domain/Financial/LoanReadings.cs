using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

/// <summary>
/// Regole di lettura delle rilevazioni dei prestiti, coerenti con quelle dei saldi manuali.
/// </summary>
public static class LoanReadings
{
    public static LoanReading? Latest(IReadOnlyList<LoanReading> readings, Guid loanId)
    {
        if (readings is null)
        {
            throw new ArgumentNullException(nameof(readings));
        }

        return readings
            .Where(reading => reading.LoanId == loanId)
            .OrderByDescending(reading => reading.ValidAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();
    }

    public static LoanReading? LatestOnOrBefore(
        IReadOnlyList<LoanReading> readings,
        Guid loanId,
        DateOnly asOfDate)
    {
        if (readings is null)
        {
            throw new ArgumentNullException(nameof(readings));
        }

        return readings
            .Where(reading => reading.LoanId == loanId && reading.ValidAt <= asOfDate)
            .OrderByDescending(reading => reading.ValidAt)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Somma degli importi dovuti alla data: per ogni prestito vale l'ultima rilevazione
    /// disponibile; un prestito senza rilevazioni contribuisce con zero.
    /// </summary>
    public static Money TotalOnOrBefore(
        IReadOnlyList<Loan> loans,
        IReadOnlyList<LoanReading> readings,
        DateOnly asOfDate,
        Currency currency)
    {
        if (loans is null)
        {
            throw new ArgumentNullException(nameof(loans));
        }

        decimal total = 0m;
        foreach (var loan in loans)
        {
            var reading = LatestOnOrBefore(readings, loan.Id, asOfDate);
            if (reading is not null)
            {
                total += reading.Amount.Amount;
            }
        }

        return new Money(total, currency);
    }
}
