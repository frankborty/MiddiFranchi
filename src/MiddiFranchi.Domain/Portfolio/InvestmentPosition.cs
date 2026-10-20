using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record InvestmentPosition
{
    public InvestmentPosition(
        InvestmentPortfolio portfolio,
        Instrument instrument,
        Quantity quantity)
    {
        if (portfolio is null)
        {
            throw new ArgumentNullException(nameof(portfolio));
        }

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

        Portfolio = portfolio;
        Instrument = instrument;
        Quantity = quantity;
    }

    public InvestmentPortfolio Portfolio { get; }

    public Instrument Instrument { get; }

    public Quantity Quantity { get; }
}
