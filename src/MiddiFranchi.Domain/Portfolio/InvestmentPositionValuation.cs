using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed record InvestmentPositionValuation
{
    private InvestmentPositionValuation(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation? priceObservation,
        ExchangeRateObservation? exchangeRateObservation,
        Money? value,
        string? incompletenessReason)
    {
        Position = position;
        ValuationDate = valuationDate;
        PriceObservation = priceObservation;
        ExchangeRateObservation = exchangeRateObservation;
        Value = value;
        IncompletenessReason = incompletenessReason;
    }

    public InvestmentPosition Position { get; }

    public DateOnly ValuationDate { get; }

    public PriceObservation? PriceObservation { get; }

    public ExchangeRateObservation? ExchangeRateObservation { get; }

    public Money? Value { get; }

    public bool IsValuated => Value is not null;

    public bool IsIncomplete => Value is null;

    public string? IncompletenessReason { get; }

    internal static InvestmentPositionValuation Valuate(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation priceObservation,
        ExchangeRateObservation? exchangeRateObservation,
        Money value) =>
        new(position, valuationDate, priceObservation, exchangeRateObservation, value, null);

    internal static InvestmentPositionValuation Incomplete(
        InvestmentPosition position,
        DateOnly valuationDate,
        PriceObservation? priceObservation,
        ExchangeRateObservation? exchangeRateObservation,
        string incompletenessReason) =>
        new(position, valuationDate, priceObservation, exchangeRateObservation, null, incompletenessReason);
}
