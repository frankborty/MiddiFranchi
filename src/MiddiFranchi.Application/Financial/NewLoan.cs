using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>Nuovo prestito; l'importo iniziale è facoltativo e, se presente, richiede la data.</summary>
public sealed record NewLoan(
    Guid AccountId,
    string Borrower,
    Money? InitialAmount,
    DateOnly? InitialDate);
