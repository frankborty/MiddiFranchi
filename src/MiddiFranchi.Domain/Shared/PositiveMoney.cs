namespace MiddiFranchi.Domain.Shared;

public readonly record struct PositiveMoney
{
    public PositiveMoney(Money value)
    {
        if (value.Amount <= 0m)
        {
            throw new ArgumentException(
                $"Importo atteso positivo, ricevuto {value}.",
                nameof(value));
        }

        Value = value;
    }

    public Money Value { get; }
}
