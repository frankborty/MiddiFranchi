namespace MiddiFranchi.Domain.Shared;

public readonly record struct UnitPrice
{
    public UnitPrice(Money price)
    {
        if (price.Amount <= 0m)
        {
            throw new ArgumentException(
                $"Il prezzo unitario deve essere positivo, ricevuto {price}.",
                nameof(price));
        }

        Price = price;
    }

    public Money Price { get; }

    public static Money operator *(UnitPrice price, Quantity quantity) =>
        Multiply(price, quantity);

    public static Money operator *(Quantity quantity, UnitPrice price) =>
        Multiply(price, quantity);

    private static Money Multiply(UnitPrice price, Quantity quantity)
    {
        if (price.Price.Currency.Code is null || price.Price.Amount <= 0m)
        {
            throw new InvalidOperationException(
                "Il prezzo unitario non è valido: è richiesto un importo positivo con valuta.");
        }

        if (quantity.Value <= 0m)
        {
            throw new InvalidOperationException(
                "La quantità non è valida: è richiesto un valore positivo.");
        }

        return new Money(price.Price.Amount * quantity.Value, price.Price.Currency);
    }

    public override string ToString() => Price.ToString();
}
