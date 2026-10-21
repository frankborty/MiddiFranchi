using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class PriceObservationTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly Guid ObservationId = Guid.Parse("5291e3fb-b4fc-463a-84c4-08a07a1c3be8");
    private static readonly Guid EtfId = Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b");
    private static readonly Guid BondId = Guid.Parse("8f31fbe7-4f8f-4b1f-a187-6c4e6f3c7c42");

    private static readonly Instrument Etf = new(
        EtfId, "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly Instrument OtherEtf = new(
        Guid.NewGuid(), "ETF alternativa", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly Instrument Bond = new(
        BondId, "Obbligazione società", InstrumentType.Bond, Euro, PriceUnit.PercentOfNominal, new Money(1000m, Euro));

    private static readonly DateOnly ValidAt = new(2026, 3, 10);
    private static readonly DateOnly AsOf = new(2026, 3, 31);
    private static readonly DateTimeOffset ObservedAt = new(2026, 3, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Construction_ShouldAcceptAUnitPriceObservation()
    {
        var observation = PriceObservation.AtUnitPrice(
            ObservationId, Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, "Manuale");

        observation.Instrument.ShouldBe(Etf);
        observation.Unit.ShouldBe(PriceUnit.PerUnit);
        observation.Price.ShouldBe(80m);
        observation.PriceCurrency.ShouldBe(Euro);
        observation.ValidAt.ShouldBe(ValidAt);
        observation.ObservedAt.ShouldBe(ObservedAt);
        observation.Source.ShouldBe("Manuale");
    }

    [Fact]
    public void Construction_ShouldAcceptAPercentOfNominalObservation()
    {
        var observation = PriceObservation.AsPercentOfNominal(
            ObservationId, Bond, 98.5m, ValidAt, ObservedAt, "STOOQ");

        observation.Unit.ShouldBe(PriceUnit.PercentOfNominal);
        observation.Price.ShouldBe(98.5m);
        observation.PriceCurrency.ShouldBeNull();
    }

    [Fact]
    public void Construction_ShouldRequireTheObservationUnitToMatchTheInstrument()
    {
        Should.Throw<ArgumentException>(
            () => PriceObservation.AsPercentOfNominal(ObservationId, Etf, 98.5m, ValidAt, ObservedAt, "STOOQ"))
            .Message.ShouldContain("stessa unità");

        Should.Throw<ArgumentException>(
            () => PriceObservation.AtUnitPrice(ObservationId, Bond, new UnitPrice(new Money(985m, Euro)), ValidAt, ObservedAt, "STOOQ"))
            .Message.ShouldContain("percentuale del nominale");
    }


    [Fact]
    public void Construction_ShouldRejectNonPositivePercentPrices()
    {
        Should.Throw<ArgumentException>(
            () => PriceObservation.AsPercentOfNominal(ObservationId, Bond, 0m, ValidAt, ObservedAt, "STOOQ"))
            .Message.ShouldContain("positiva");

        Should.Throw<ArgumentException>(
            () => PriceObservation.AsPercentOfNominal(ObservationId, Bond, -1m, ValidAt, ObservedAt, "STOOQ"))
            .Message.ShouldContain("positiva");
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifierOrABlankSource()
    {
        Should.Throw<ArgumentException>(
            () => PriceObservation.AtUnitPrice(Guid.Empty, Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, "Manuale"))
            .ParamName.ShouldBe("id");

        Should.Throw<ArgumentException>(
            () => PriceObservation.AtUnitPrice(ObservationId, Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, ""))
            .ParamName.ShouldBe("source");

        Should.Throw<ArgumentException>(
            () => PriceObservation.AsPercentOfNominal(ObservationId, Bond, 98.5m, ValidAt, ObservedAt, "   "))
            .ParamName.ShouldBe("source");
    }

    [Fact]
    public void Consultation_ShouldPickTheMostRecentObservationAvailableOnOrBeforeTheRequestedDate()
    {
        var older = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(70m, Euro)), new DateOnly(2026, 3, 5), ObservedAt, "STOOQ");
        var sameDay = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, "Manuale");

        var observation = PriceObservations.MostRecentOnOrBefore([older, sameDay], Etf, new DateOnly(2026, 3, 31));

        observation.ShouldNotBeNull();
        observation!.Price.ShouldBe(80m);
    }

    [Fact]
    public void Consultation_ShouldIgnoreFutureObservations()
    {
        var future = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(90m, Euro)), new DateOnly(2026, 4, 15), ObservedAt, "STOOQ");

        PriceObservations.MostRecentOnOrBefore([future], Etf, AsOf).ShouldBeNull();
    }

    [Fact]
    public void Consultation_ShouldReturnNothingWhenNoPriceIsAvailableForTheInstrument()
    {
        PriceObservations.MostRecentOnOrBefore([], Etf, AsOf).ShouldBeNull();
    }

    [Fact]
    public void Consultation_ShouldIgnoreObservationsOfOtherInstruments()
    {
        var otherInstrument = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), OtherEtf, new UnitPrice(new Money(985m, Euro)), ValidAt, ObservedAt, "STOOQ");
        var expected = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, "STOOQ");

        var observation = PriceObservations.MostRecentOnOrBefore([otherInstrument, expected], Etf, AsOf);

        observation!.Instrument.ShouldBe(Etf);
        observation.Price.ShouldBe(80m);
    }

    [Fact]
    public void Consultation_ShouldRejectNullArguments()
    {
        Should.Throw<ArgumentNullException>(
                () => PriceObservations.MostRecentOnOrBefore(null!, Etf, AsOf))
            .ParamName.ShouldBe("observations");

        Should.Throw<ArgumentNullException>(
                () => PriceObservations.MostRecentOnOrBefore([], null!, AsOf))
            .ParamName.ShouldBe("instrument");
    }

    [Fact]
    public void Consultation_ShouldBreakTiesDeterministicallyWhenValidAtAndObservedAtMatch()
    {
        var first = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), ValidAt, ObservedAt, "AAA");
        var second = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(90m, Euro)), ValidAt, ObservedAt, "ZZZ");

        var forward = PriceObservations.MostRecentOnOrBefore([first, second], Etf, AsOf);
        var backward = PriceObservations.MostRecentOnOrBefore([second, first], Etf, AsOf);

        forward.ShouldBe(second);
        backward.ShouldBe(second);
    }

    [Fact]
    public void Consultation_ShouldKeepObservationsAppendOnlyAndPickTheLastAcquiredWhenDatesMatch()
    {
        var earlier = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(71m, Euro)), ValidAt, ObservedAt, "STOOQ");
        var laterObserved = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(82m, Euro)), ValidAt, ObservedAt.AddHours(3), "Manuale");

        var observation = PriceObservations.MostRecentOnOrBefore([earlier, laterObserved], Etf, AsOf);

        observation!.Price.ShouldBe(82m);
        observation.Source.ShouldBe("Manuale");
    }
}
