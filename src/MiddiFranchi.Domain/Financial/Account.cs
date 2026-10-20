using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Account
{
    public Account(Guid id, string name, AccountType type, Currency currency, AccountStatus status)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo del conto non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome del conto è obbligatorio.", nameof(name));
        }

        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta del conto è obbligatoria.", nameof(currency));
        }

        Id = id;
        Name = name;
        Type = type;
        Currency = currency;
        Status = status;
    }

    public Guid Id { get; }

    public string Name { get; }

    public AccountType Type { get; }

    public Currency Currency { get; }

    public AccountStatus Status { get; }
}
