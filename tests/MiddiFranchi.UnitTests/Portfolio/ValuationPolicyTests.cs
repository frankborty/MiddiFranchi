using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class ValuationPolicyTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Instrument Etf = new(
        Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly DateTimeOffset ObservedAt = new(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);

    [Fact]
    public void Unbounded_ShouldNeverMarkPricesAsStale()
    {
        ValuationPolicy.Unbounded
            .IsPriceStale(Price(new DateOnly(2025, 1, 1)), ValuationDate)
            .ShouldBeFalse();
    }

    [Fact]
    public void Staleness_ShouldUseTheEconomicDateNotTheAcquisitionTime()
    {
        var oldQuoteObservedRecently = PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            Etf,
            new UnitPrice(new Money(80m, Euro)),
            new DateOnly(2026, 3, 1),
            new DateTimeOffset(2026, 3, 30, 18, 0, 0, TimeSpan.Zero),
            "Manuale");
        var policy = new ValuationPolicy(maximumPriceAgeDays: 5);

        policy.IsPriceStale(oldQuoteObservedRecently, ValuationDate).ShouldBeTrue();
    }

    [Fact]
    public void Threshold_ShouldBeInclusiveAtTheBoundary()
    {
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        policy.IsPriceStale(Price(new DateOnly(2026, 3, 21)), ValuationDate).ShouldBeFalse();
        policy.IsPriceStale(Price(new DateOnly(2026, 3, 20)), ValuationDate).ShouldBeTrue();
    }

    [Fact]
    public void Construction_ShouldRejectANegativeThreshold()
    {
        Should.Throw<ArgumentOutOfRangeException>(
                () => new ValuationPolicy(maximumPriceAgeDays: -1))
            .ParamName.ShouldBe("maximumPriceAgeDays");
    }

    private static PriceObservation Price(DateOnly validAt) =>
        PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), validAt, ObservedAt, "Manuale");
}
