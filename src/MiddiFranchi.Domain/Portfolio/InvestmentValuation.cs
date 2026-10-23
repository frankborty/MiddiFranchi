namespace MiddiFranchi.Domain.Portfolio;

public sealed record InvestmentValuation
{
    public InvestmentValuation(DateOnly valuationDate, IReadOnlyList<InvestmentPositionValuation> positions)
    {
        if (positions is null)
        {
            throw new ArgumentNullException(nameof(positions));
        }

        ValuationDate = valuationDate;
        Positions = positions.ToArray();
    }

    public DateOnly ValuationDate { get; }

    public IReadOnlyList<InvestmentPositionValuation> Positions { get; }

    public bool IsComplete => Positions.All(position => position.IsValuated);

    public bool IsIncomplete => !IsComplete;
}
