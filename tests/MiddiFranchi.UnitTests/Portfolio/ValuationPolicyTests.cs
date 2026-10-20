using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class ValuationPolicyTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Instrument Etf = new(
        Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly DateTimeOffset ObservedAt = new(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);

    [Fact]
    public void Unbounded_ShouldNeverMarkObservationsAsStale()
    {
        var price = Price(new DateOnly(2025, 1, 1));
        var rate = Rate(new DateOnly(2025, 1, 1));

        ValuationPolicy.Unbounded.IsPriceStale(price, ValuationDate).ShouldBeFalse();
        ValuationPolicy.Unbounded.IsExchangeRateStale(rate, ValuationDate).ShouldBeFalse();
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
        var oldRateObservedRecently = new ExchangeRateObservation(
            Guid.NewGuid(),
            new ExchangeRate(Usd, 1.10m),
            new DateOnly(2026, 3, 1),
            new DateTimeOffset(2026, 3, 30, 18, 0, 0, TimeSpan.Zero),
            "ECB");
        var policy = new ValuationPolicy(maximumPriceAgeDays: 5, maximumExchangeRateAgeDays: 5);

        policy.IsPriceStale(oldQuoteObservedRecently, ValuationDate).ShouldBeTrue();
        policy.IsExchangeRateStale(oldRateObservedRecently, ValuationDate).ShouldBeTrue();
    }

    [Fact]
    public void Thresholds_ShouldBeIndependentAndInclusiveAtTheBoundary()
    {
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10, maximumExchangeRateAgeDays: 31);

        policy.IsPriceStale(Price(new DateOnly(2026, 3, 21)), ValuationDate).ShouldBeFalse();
        policy.IsPriceStale(Price(new DateOnly(2026, 3, 20)), ValuationDate).ShouldBeTrue();
        policy.IsExchangeRateStale(Rate(new DateOnly(2026, 2, 28)), ValuationDate).ShouldBeFalse();
        policy.IsExchangeRateStale(Rate(new DateOnly(2026, 2, 27)), ValuationDate).ShouldBeTrue();
    }

    [Fact]
    public void PriceThreshold_ShouldNotAffectExchangeRateFreshness()
    {
        var policy = new ValuationPolicy(maximumPriceAgeDays: 5, maximumExchangeRateAgeDays: 60);

        policy.IsExchangeRateStale(Rate(new DateOnly(2026, 3, 1)), ValuationDate).ShouldBeFalse();
        policy.IsPriceStale(Price(new DateOnly(2026, 3, 1)), ValuationDate).ShouldBeTrue();
    }

    [Fact]
    public void Construction_ShouldRejectNegativeThresholds()
    {
        Should.Throw<ArgumentOutOfRangeException>(
                () => new ValuationPolicy(maximumPriceAgeDays: -1))
            .ParamName.ShouldBe("maximumPriceAgeDays");

        Should.Throw<ArgumentOutOfRangeException>(
                () => new ValuationPolicy(maximumExchangeRateAgeDays: -1))
            .ParamName.ShouldBe("maximumExchangeRateAgeDays");
    }

    private static PriceObservation Price(DateOnly validAt) =>
        PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), validAt, ObservedAt, "Manuale");

    private static ExchangeRateObservation Rate(DateOnly validAt) =>
        new(Guid.NewGuid(), new ExchangeRate(Usd, 1.10m), validAt, ObservedAt, "ECB");
}
