using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class PortfolioValuationCalculatorTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly InvestmentPortfolio Portfolio = new(
        Guid.Parse("9a5f8b83-c3d4-4a01-9f9f-4c8a0d3f1e2d"), "Portafoglio principale", Euro);
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly Instrument Bond = new(
        Guid.Parse("8f31fbe7-4f8f-4b1f-a187-6c4e6f3c7c42"),
        "Obbligazione società",
        InstrumentType.Bond,
        Euro,
        PriceUnit.PercentOfNominal,
        new Money(1000m, Euro));
    private static readonly Account Account = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valuate_ShouldComputeTheMarketValueWithTheLatestPriceOnOrBeforeTheValuationDate()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = Price(Etf, 70m, new DateOnly(2026, 3, 5));
        var currentPrice = Price(Etf, 80m, new DateOnly(2026, 3, 18));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice, currentPrice], ValuationDate);

        valuation.IsComplete.ShouldBeTrue();
        var position = valuation.Positions.ShouldHaveSingleItem();
        position.PriceObservation.ShouldBe(currentPrice);
        position.Value!.Value.ShouldBe(new Money(200m, Euro));
    }

    [Fact]
    public void Valuate_ShouldUseThePriceAvailableWhenTheLatestOneExceedsTheValuationDate()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var olderPrice = Price(Etf, 70m, new DateOnly(2026, 3, 5));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice], new DateOnly(2026, 3, 10));

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.PriceObservation.ShouldBe(olderPrice);
        position.Value!.Value.ShouldBe(new Money(175m, Euro));
    }

    [Fact]
    public void Valuate_ShouldComputeTheEffectiveUnitPriceForBondsQuotedAsPercentOfNominal()
    {
        var operation = Buy(Bond, 3m, new DateOnly(2026, 3, 10));
        var price = PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(), Bond, 98.5m, new DateOnly(2026, 3, 15), RecordedAt, "Mercato obbligazionario");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [price], ValuationDate);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.Value!.Value.ShouldBe(new Money(2955.00m, Euro));
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionsIncompleteWhenNoPriceIsAvailable()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [], ValuationDate);

        valuation.IsComplete.ShouldBeFalse();
        valuation.IsIncomplete.ShouldBeTrue();
        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsIncomplete.ShouldBeTrue();
        position.Value.ShouldBeNull();
        position.IncompletenessReason.ShouldNotBeNull();
        position.IncompletenessReason!.ShouldContain("quotazione");
    }

    [Fact]
    public void Valuate_ShouldIgnoreFuturePricesAndTreatThePositionAsIncomplete()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var futurePrice = Price(Etf, 80m, new DateOnly(2026, 4, 10));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [futurePrice], ValuationDate);

        valuation.IsIncomplete.ShouldBeTrue();
        valuation.Positions.ShouldHaveSingleItem().IncompletenessReason!.ShouldContain("quotazione");
    }

    [Fact]
    public void Valuate_ShouldBeIndependentFromTheInputOrder()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = Price(Etf, 70m, new DateOnly(2026, 3, 5));
        var currentPrice = Price(Etf, 80m, new DateOnly(2026, 3, 18));

        var forward = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice, currentPrice], ValuationDate);
        var backward = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [currentPrice, olderPrice], ValuationDate);

        backward.Positions[0].PriceObservation.ShouldBe(currentPrice);
        backward.Positions[0].Value!.Value.ShouldBe(new Money(200m, Euro));
        forward.Positions[0].PriceObservation.ShouldBe(currentPrice);
        forward.Positions[0].Value!.Value.ShouldBe(new Money(200m, Euro));
    }

    [Fact]
    public void Valuate_ShouldNotMutateTheProvidedCollections()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = Price(Etf, 70m, new DateOnly(2026, 3, 5));
        var currentPrice = Price(Etf, 80m, new DateOnly(2026, 3, 18));
        var priceObservations = new List<PriceObservation> { currentPrice, olderPrice };

        PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], priceObservations, ValuationDate);

        priceObservations.Count.ShouldBe(2);
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionIncompleteWhenTheLatestPriceIsStale()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var stalePrice = Price(Etf, 80m, new DateOnly(2026, 3, 1));
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [stalePrice], ValuationDate, policy);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsIncomplete.ShouldBeTrue();
        position.Value.ShouldBeNull();
        position.PriceObservation.ShouldBe(stalePrice);
        position.IncompletenessReason.ShouldNotBeNull();
        position.IncompletenessReason!.ShouldContain("obsoleta");
        position.IncompletenessReason.ShouldContain("soglia 10");
    }

    [Fact]
    public void Valuate_ShouldAcceptAPriceExactlyAtTheConfiguredAgeLimit()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var priceAtLimit = Price(Etf, 80m, new DateOnly(2026, 3, 21));
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceAtLimit], ValuationDate, policy);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsValuated.ShouldBeTrue();
        position.Value!.Value.ShouldBe(new Money(200m, Euro));
    }

    [Fact]
    public void Valuate_ShouldKeepTheLatestObservedQuoteBeforeCheckingStaleness()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var earlier = Price(Etf, 80m, new DateOnly(2026, 3, 1));
        var later = PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            Etf,
            new UnitPrice(new Money(90m, Euro)),
            new DateOnly(2026, 3, 1),
            RecordedAt.AddHours(3),
            "Manuale");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio,
            [operation],
            [earlier, later],
            ValuationDate,
            new ValuationPolicy(maximumPriceAgeDays: 5));

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.PriceObservation.ShouldBe(later);
        position.IncompletenessReason!.ShouldContain("obsoleta");
    }

    [Fact]
    public void Valuate_ShouldMatchTheCapitalPostingForABondQuotedAsPercentOfNominal()
    {
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            Portfolio,
            Bond,
            Account,
            new Quantity(3m),
            QuotedPrice.AsPercentOfNominal(98.5m),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);
        var price = PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(), Bond, 98.5m, new DateOnly(2026, 3, 15), RecordedAt, "Mercato obbligazionario");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [price], ValuationDate);

        var positionValue = valuation.Positions.ShouldHaveSingleItem().Value!.Value;
        positionValue.ShouldBe(new Money(2955m, Euro));
        var capital = operation.GeneratePostings().Single(posting => posting.Kind == PostingKind.Capital);
        capital.SignedAmount.ShouldBe(-positionValue.Amount);
    }

    [Fact]
    public void Valuate_ShouldRejectNullCollections()
    {
        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(null!, [], [], ValuationDate))
            .ParamName.ShouldBe("portfolio");

        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(Portfolio, null!, [], ValuationDate))
            .ParamName.ShouldBe("operations");

        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(Portfolio, [], null!, ValuationDate))
            .ParamName.ShouldBe("priceObservations");
    }

    private static InvestmentOperation Buy(Instrument instrument, decimal quantity, DateOnly date) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            Portfolio,
            instrument,
            Account,
            new Quantity(quantity),
            instrument.PriceUnit == PriceUnit.PercentOfNominal
                ? QuotedPrice.AsPercentOfNominal(98.5m)
                : QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))),
            null,
            date,
            RecordedAt);

    private static PriceObservation Price(Instrument instrument, decimal price, DateOnly validAt) =>
        PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(price, Euro)),
            validAt,
            RecordedAt,
            "Test");
}
