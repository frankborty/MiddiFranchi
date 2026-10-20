using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class ExchangeRateObservationTests
{
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Currency Gbp = Currency.Parse("GBP");
    private static readonly Guid ObservationId = Guid.Parse("5291e3fb-b4fc-463a-84c4-08a07a1c3be8");
    private static readonly DateOnly ValidAt = new(2026, 3, 10);
    private static readonly DateTimeOffset ObservedAt = new(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Construction_ShouldAcceptAChangeObservation()
    {
        var observation = new ExchangeRateObservation(
            ObservationId, new ExchangeRate(Usd, 1.10m), ValidAt, ObservedAt, "ECB");

        observation.Rate.Currency.ShouldBe(Usd);
        observation.Rate.Rate.ShouldBe(1.10m);
        observation.ValidAt.ShouldBe(ValidAt);
        observation.ObservedAt.ShouldBe(ObservedAt);
        observation.Source.ShouldBe("ECB");
    }

    [Fact]
    public void Construction_ShouldRejectAnIncompleteRate()
    {
        Should.Throw<ArgumentException>(
            () => new ExchangeRateObservation(ObservationId, default, ValidAt, ObservedAt, "ECB"))
            .ParamName.ShouldBe("rate");
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifierOrABlankSource()
    {
        Should.Throw<ArgumentException>(
            () => new ExchangeRateObservation(Guid.Empty, new ExchangeRate(Usd, 1.10m), ValidAt, ObservedAt, "ECB"))
            .ParamName.ShouldBe("id");

        Should.Throw<ArgumentException>(
            () => new ExchangeRateObservation(ObservationId, new ExchangeRate(Usd, 1.10m), ValidAt, ObservedAt, "  "))
            .ParamName.ShouldBe("source");
    }

    [Fact]
    public void Consultation_ShouldPickTheMostRecentObservationAvailableOnOrBeforeTheRequestedDate()
    {
        var older = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.05m), new DateOnly(2026, 3, 5), ObservedAt, "ECB");
        var sameDay = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.10m), ValidAt, ObservedAt, "Manuale");

        var observation = ExchangeRateObservations
            .MostRecentOnOrBefore([older, sameDay], Usd, new DateOnly(2026, 3, 31));

        observation!.Rate.Rate.ShouldBe(1.10m);
    }

    [Fact]
    public void Consultation_ShouldIgnoreObservationsOfOtherCurrenciesAndFutureDates()
    {
        var future = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.20m), new DateOnly(2026, 4, 15), ObservedAt, "Manuale");
        var otherCurrency = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Gbp, 0.85m), ValidAt, ObservedAt, "Manuale");

        ExchangeRateObservations.MostRecentOnOrBefore([future, otherCurrency], Usd, ValidAt).ShouldBeNull();
    }

    [Fact]
    public void Consultation_ShouldRejectANullObservationList()
    {
        Should.Throw<ArgumentNullException>(
                () => ExchangeRateObservations.MostRecentOnOrBefore(null!, Usd, ValidAt))
            .ParamName.ShouldBe("observations");
    }

    [Fact]
    public void Consultation_ShouldBreakTiesDeterministicallyWhenValidAtAndObservedAtMatch()
    {
        var first = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.05m), ValidAt, ObservedAt, "AAA");
        var second = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.11m), ValidAt, ObservedAt, "ZZZ");

        var forward = ExchangeRateObservations.MostRecentOnOrBefore([first, second], Usd, ValidAt);
        var backward = ExchangeRateObservations.MostRecentOnOrBefore([second, first], Usd, ValidAt);

        forward.ShouldBe(second);
        backward.ShouldBe(second);
    }

    [Fact]
    public void Consultation_ShouldRemainDeterministicWhenObservationsBelongToTheSameDate()
    {
        var earlier = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.05m), ValidAt, ObservedAt, "ECB");
        var laterAcquired = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Usd, 1.11m), ValidAt, ObservedAt.AddHours(2), "Manuale");

        var observation = ExchangeRateObservations
            .MostRecentOnOrBefore([laterAcquired, earlier], Usd, ValidAt);

        observation!.Rate.Rate.ShouldBe(1.11m);
        observation.Source.ShouldBe("Manuale");
    }
}
