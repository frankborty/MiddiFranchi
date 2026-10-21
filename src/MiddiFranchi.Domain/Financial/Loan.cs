namespace MiddiFranchi.Domain.Financial;

/// <summary>
/// Prestito concesso a una persona, associato a un conto di tipo <see cref="AccountType.Loans"/>.
/// Il saldo del prestito è gestito esclusivamente dalle sue rilevazioni.
/// </summary>
public sealed class Loan
{
    public Loan(Guid id, Account account, string borrower, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo del prestito non può essere vuoto.", nameof(id));
        }

        if (account is null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        if (account.Type != AccountType.Loans)
        {
            throw new ArgumentException(
                "Un prestito può essere associato solo a un conto di tipo Prestiti.",
                nameof(account));
        }

        if (string.IsNullOrWhiteSpace(borrower))
        {
            throw new ArgumentException("Il nome del debitore è obbligatorio.", nameof(borrower));
        }

        Id = id;
        Account = account;
        Borrower = borrower.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Account Account { get; }

    public Guid AccountId => Account.Id;

    /// <summary>Persona a cui è stato concesso il prestito.</summary>
    public string Borrower { get; }

    public DateTimeOffset CreatedAt { get; }
}
