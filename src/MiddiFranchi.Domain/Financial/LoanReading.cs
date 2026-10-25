using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

/// <summary>
/// Rilevazione manuale dell'importo ancora dovuto per un singolo prestito.
/// </summary>
public sealed class LoanReading
{
    public LoanReading(
        Guid id,
        Loan loan,
        Money amount,
        DateOnly validAt,
        DateTimeOffset recordedAt,
        string? note = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo della rilevazione non può essere vuoto.", nameof(id));
        }

        if (loan is null)
        {
            throw new ArgumentNullException(nameof(loan));
        }

        if (amount.Currency != loan.Account.Currency)
        {
            throw new ArgumentException(
                $"La valuta della rilevazione ({amount.Currency.Code}) deve coincidere con la valuta del conto ({loan.Account.Currency.Code}).",
                nameof(amount));
        }

        Id = id;
        Loan = loan;
        Amount = amount;
        ValidAt = validAt;
        RecordedAt = recordedAt;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; }

    public Loan Loan { get; }

    public Guid LoanId => Loan.Id;

    /// <summary>Importo ancora dovuto alla data della rilevazione.</summary>
    public Money Amount { get; }

    /// <summary>Nota facoltativa, per esempio la causale di una restituzione.</summary>
    public string? Note { get; }

    public DateOnly ValidAt { get; }

    public DateTimeOffset RecordedAt { get; }
}
