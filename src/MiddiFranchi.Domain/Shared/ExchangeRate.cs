using System.Globalization;

namespace MiddiFranchi.Domain.Shared;

public readonly record struct ExchangeRate
{
    private static readonly Currency Euro = Currency.Parse("EUR");

    public ExchangeRate(Currency currency, decimal rate)
    {
        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta del tasso è obbligatoria.", nameof(currency));
        }

        if (currency == Euro)
        {
            throw new ArgumentException(
                "Il tasso si riferisce a una valuta estera rispetto a EUR: non si definisce un tasso per EUR.",
                nameof(currency));
        }

        if (rate <= 0m)
        {
            throw new ArgumentException(
                $"Il tasso di cambio deve essere positivo, ricevuto {rate.ToString(CultureInfo.InvariantCulture)}.",
                nameof(rate));
        }

        Currency = currency;
        Rate = rate;
    }

    public Currency Currency { get; }

    public decimal Rate { get; }

    public Money Convert(Money amount, Currency target)
    {
        if (Currency.Code is null || Rate <= 0m)
        {
            throw new InvalidOperationException(
                "Il tasso di cambio non è valido: è richiesta una valuta estera e un tasso positivo.");
        }

        if (amount.Currency == target)
        {
            return amount;
        }

        if (amount.Currency == Euro && target == Currency)
        {
            return new Money(amount.Amount * Rate, target);
        }

        if (amount.Currency == Currency && target == Euro)
        {
            return new Money(amount.Amount / Rate, target);
        }

        throw new InvalidOperationException(
            $"Coppia di valute non coerente con il tasso {Currency.Code}/EUR: {amount.Currency.Code} → {target.Code}.");
    }

    public override string ToString() => $"{Rate.ToString(CultureInfo.InvariantCulture)} {Currency.Code} per EUR";
}
