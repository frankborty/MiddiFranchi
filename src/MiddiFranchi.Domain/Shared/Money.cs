using System.Globalization;

namespace MiddiFranchi.Domain.Shared;

public readonly record struct Money
{
    public Money(decimal amount, Currency currency)
    {
        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta dell'importo è obbligatoria.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Somma tra valute diverse non consentita: {left.Currency.Code} e {right.Currency.Code}.");
        }

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Sottrazione tra valute diverse non consentita: {left.Currency.Code} e {right.Currency.Code}.");
        }

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money left, decimal factor) =>
        new(left.Amount * factor, left.Currency);

    public static Money operator *(decimal factor, Money right) => right * factor;

    public override string ToString() => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency.Code}";
}
