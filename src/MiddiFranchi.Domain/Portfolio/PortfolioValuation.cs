namespace MiddiFranchi.Domain.Portfolio;

public sealed record PortfolioValuation
{
    public PortfolioValuation(
        InvestmentPortfolio portfolio,
        DateOnly valuationDate,
        IReadOnlyList<InvestmentPositionValuation> positions)
    {
        if (portfolio is null)
        {
            throw new ArgumentNullException(nameof(portfolio));
        }

        if (positions is null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        Portfolio = portfolio;
        ValuationDate = valuationDate;
        Positions = positions.ToArray();
    }

    public InvestmentPortfolio Portfolio { get; }

    public DateOnly ValuationDate { get; }

    public IReadOnlyList<InvestmentPositionValuation> Positions { get; }

    public bool IsComplete => Positions.All(position => position.IsValuated);

    public bool IsIncomplete => !IsComplete;
}
