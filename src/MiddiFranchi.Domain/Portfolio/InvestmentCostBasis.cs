using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Costo residuo di una posizione in EUR, commissioni d'acquisto incluse.
/// </summary>
public sealed record InvestmentCostBasis
{
    public InvestmentCostBasis(Instrument instrument, Quantity quantity, Money costBasis)
    {
        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        if (quantity.Value <= 0m)
        {
            throw new ArgumentException(
                "Il costo residuo richiede una quantità positiva.",
                nameof(quantity));
        }

        if (costBasis.Currency != instrument.QuoteCurrency)
        {
            throw new ArgumentException(
                "La valuta del costo deve coincidere con la valuta di quotazione dello strumento.",
                nameof(costBasis));
        }

        Instrument = instrument;
        Quantity = quantity;
        CostBasis = costBasis;
        AverageCost = new Money(costBasis.Amount / quantity.Value, instrument.QuoteCurrency);
    }

    public Instrument Instrument { get; }

    public Quantity Quantity { get; }

    public Money CostBasis { get; }

    public Money AverageCost { get; }
}
