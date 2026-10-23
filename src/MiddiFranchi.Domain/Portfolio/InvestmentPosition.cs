using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record InvestmentPosition
{
    public InvestmentPosition(Instrument instrument, Quantity quantity)
    {
        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        if (quantity.Value <= 0m)
        {
            throw new ArgumentException(
                $"La quantità della posizione deve essere positiva, ricevuto {quantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                nameof(quantity));
        }

        Instrument = instrument;
        Quantity = quantity;
    }

    public Instrument Instrument { get; }

    public Quantity Quantity { get; }
}
