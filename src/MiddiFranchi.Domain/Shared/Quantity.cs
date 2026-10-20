namespace MiddiFranchi.Domain.Shared;

public readonly record struct Quantity
{
    public Quantity(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentException(
                $"La quantità deve essere positiva, ricevuto {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                nameof(value));
        }

        Value = value;
    }

    public decimal Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
