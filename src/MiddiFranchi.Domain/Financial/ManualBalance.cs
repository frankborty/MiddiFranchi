using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class ManualBalance
{
    public ManualBalance(
        Guid id,
        Account account,
        Money amount,
        DateOnly validAt,
        DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo della rilevazione non può essere vuoto.", nameof(id));
        }

        if (account is null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        if (amount.Currency.Code is null)
        {
            throw new ArgumentException("La valuta della rilevazione è obbligatoria.", nameof(amount));
        }

        if (amount.Currency != account.Currency)
        {
            throw new ArgumentException(
                $"La valuta della rilevazione ({amount.Currency.Code}) deve coincidere con la valuta del conto ({account.Currency.Code}).",
                nameof(amount));
        }

        Id = id;
        Account = account;
        Amount = amount;
        ValidAt = validAt;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; }

    public Account Account { get; }

    public Guid AccountId => Account.Id;

    /// <summary>Saldo assoluto osservato, nella valuta del conto; può essere negativo o zero.</summary>
    public Money Amount { get; }

    /// <summary>Data economica a cui la rilevazione si riferisce.</summary>
    public DateOnly ValidAt { get; }

    /// <summary>Istante di registrazione della rilevazione.</summary>
    public DateTimeOffset RecordedAt { get; }
}
