using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>Prestito con la sua ultima rilevazione disponibile e lo storico completo.</summary>
public sealed record LoanOverview(
    Loan Loan,
    Money? CurrentAmount,
    DateOnly? LatestDate,
    IReadOnlyList<LoanReading> Readings)
{
    public bool HasReading => CurrentAmount is not null;

    /// <summary>Un prestito senza rilevazioni è attivo; si disattiva quando l'importo scende a zero.</summary>
    public bool IsActive => CurrentAmount is null || CurrentAmount.Value.Amount != 0m;
}
