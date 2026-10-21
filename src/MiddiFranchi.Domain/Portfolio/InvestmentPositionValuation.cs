using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record InvestmentPositionValuation
{
    private InvestmentPositionValuation(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation? priceObservation,
        Money? value,
        string? incompletenessReason)
    {
        Position = position;
        ValuationDate = valuationDate;
        PriceObservation = priceObservation;
        Value = value;
        IncompletenessReason = incompletenessReason;
    }

    public InvestmentPosition Position { get; }

    public DateOnly ValuationDate { get; }

    public PriceObservation? PriceObservation { get; }

    public Money? Value { get; }

    public bool IsValuated => Value is not null;

    public bool IsIncomplete => Value is null;

    public string? IncompletenessReason { get; }

    internal static InvestmentPositionValuation Valuate(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation priceObservation,
        Money value) =>
        new(position, valuationDate, priceObservation, value, null);

    internal static InvestmentPositionValuation Incomplete(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation? priceObservation,
        string incompletenessReason) =>
        new(position, valuationDate, priceObservation, null, incompletenessReason);
}
