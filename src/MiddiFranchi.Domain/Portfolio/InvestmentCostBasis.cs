using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Costo residuo di una posizione (Portfolio, Instrument) in EUR, commissioni d'acquisto incluse.
/// </summary>
public sealed record InvestmentCostBasis
{
    public InvestmentCostBasis(
        InvestmentPortfolio portfolio,
        Instrument instrument,
        Quantity quantity,
        Money costBasis)
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
                "Il costo residuo richiede una quantità positiva.",
                nameof(quantity));
        }

        if (costBasis.Currency != instrument.QuoteCurrency)
        {
            throw new ArgumentException(
                "La valuta del costo deve coincidere con la valuta di quotazione dello strumento.",
                nameof(costBasis));
        }

        Portfolio = portfolio;
        Instrument = instrument;
        Quantity = quantity;
        CostBasisQuote = costBasis;
        CostBasisReference = costBasis;
        CostBasisEur = costBasis;
        AverageCostQuote = new Money(costBasis.Amount / quantity.Value, instrument.QuoteCurrency);
    }

    public InvestmentPortfolio Portfolio { get; }

    public Instrument Instrument { get; }

    public Quantity Quantity { get; }

    public Money CostBasisQuote { get; }

    public Money? CostBasisReference { get; }

    public Money? CostBasisEur { get; }

    public Money AverageCostQuote { get; }

    public string? ReferenceIncompletenessReason => null;

    public string? EurIncompletenessReason => null;

    public bool IsComplete => true;
}
