using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Portfolio;

public class PortfolioValuationCalculatorTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly InvestmentPortfolio Portfolio = new(
        Guid.Parse("9a5f8b83-c3d4-4a01-9f9f-4c8a0d3f1e2d"), "Portafoglio principale", Euro);
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", InstrumentType.Etf, Euro, PriceUnit.PerUnit);
    private static readonly Instrument UsdInstrument = new(
        Guid.Parse("c8e4a2b7-71d5-4f30-a9c6-2bd7fbcf7a94"), "Azioni USA", InstrumentType.Share, Usd, PriceUnit.PerUnit);
    private static readonly Instrument Bond = new(
        Guid.Parse("8f31fbe7-4f8f-4b1f-a187-6c4e6f3c7c42"),
        "Obbligazione società",
        InstrumentType.Bond,
        Euro,
        PriceUnit.PercentOfNominal,
        new Money(1000m, Euro));
    private static readonly Account EuroAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account UsdAccount = new(
        Guid.Parse("9ae1be54-3eef-4971-9ad4-d58ca44dd7cb"), "Conto valute", AccountType.Checking, Usd, AccountStatus.Active);
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valuate_ShouldComputeTheMarketValueWithTheLatestPriceOnOrBeforeTheValuationDate()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(70m, Euro)), new DateOnly(2026, 3, 5), RecordedAt, "STOOQ");
        var currentPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 18), RecordedAt, "Manuale");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice, currentPrice], [], ValuationDate);

        valuation.IsComplete.ShouldBeTrue();
        var position = valuation.Positions.ShouldHaveSingleItem();
        position.PriceObservation.ShouldBe(currentPrice);
        position.Value!.Value.ShouldBe(new Money(200m, Euro));
        position.ExchangeRateObservation.ShouldBeNull();
    }

    [Fact]
    public void Valuate_ShouldUseThePriceAvailableWhenTheLatestOneExceedsTheValuationDate()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var olderPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(70m, Euro)), new DateOnly(2026, 3, 5), RecordedAt, "STOOQ");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice], [], new DateOnly(2026, 3, 10));

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.PriceObservation.ShouldBe(olderPrice);
        position.Value!.Value.ShouldBe(new Money(175m, Euro));
    }

    [Fact]
    public void Valuate_ShouldConvertForeignPositionsUsingTheExchangeRateConvention()
    {
        var operation = Buy(UsdInstrument, 5m, new DateOnly(2026, 3, 10));
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(22m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");
        var rate = ExchangeRateObservation(1.10m, new DateOnly(2026, 3, 15));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceInUsd], [rate], ValuationDate);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.ExchangeRateObservation.ShouldBe(rate);
        position.PriceObservation.ShouldBe(priceInUsd);
        position.Value!.Value.ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Valuate_ShouldComputeTheEffectiveUnitPriceForBondsQuotedAsPercentOfNominal()
    {
        var operation = Buy(Bond, 3m, new DateOnly(2026, 3, 10));
        var price = PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(), Bond, 98.5m, new DateOnly(2026, 3, 15), RecordedAt, "Mercato obbligazionario");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [price], [], ValuationDate);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.ExchangeRateObservation.ShouldBeNull();
        position.Value!.Value.ShouldBe(new Money(2955.00m, Euro));
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionsIncompleteWhenNoPriceIsAvailable()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [], [], ValuationDate);

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
        var futurePrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 4, 10), RecordedAt, "STOOQ");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [futurePrice], [], ValuationDate);

        valuation.IsIncomplete.ShouldBeTrue();
        valuation.Positions.ShouldHaveSingleItem().IncompletenessReason!.ShouldContain("quotazione");
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionsIncompleteWhenTheExchangeRateIsMissing()
    {
        var operation = Buy(UsdInstrument, 5m, new DateOnly(2026, 3, 10));
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(22m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceInUsd], [], ValuationDate);

        valuation.IsComplete.ShouldBeFalse();
        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsIncomplete.ShouldBeTrue();
        position.Value.ShouldBeNull();
        position.IncompletenessReason!.ShouldContain("cambio");
    }

    [Fact]
    public void Valuate_ShouldIgnoreFutureExchangeRateObservations()
    {
        var operation = Buy(UsdInstrument, 5m, new DateOnly(2026, 3, 10));
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(22m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");
        var futureRate = ExchangeRateObservation(1.10m, new DateOnly(2026, 4, 15));

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceInUsd], [futureRate], ValuationDate);

        valuation.IsIncomplete.ShouldBeTrue();
        valuation.Positions.ShouldHaveSingleItem().IncompletenessReason!.ShouldContain("cambio");
    }

    [Fact]
    public void Valuate_ShouldUseThePortfolioReferenceCurrencyForForeignInstrumentsInTheSameCurrency()
    {
        var usdPortfolio = new InvestmentPortfolio(
            Guid.Parse("5b1f5a55-3a5b-4a3f-ac11-2bcbcb9ce6cb"), "Portafoglio USA", Usd);
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            usdPortfolio,
            UsdInstrument,
            UsdAccount,
            new Quantity(1m),
            Quote(80m, Usd),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(90m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");

        var valuation = PortfolioValuationCalculator.Valuate(
            usdPortfolio, [operation], [priceInUsd], [], ValuationDate);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.ExchangeRateObservation.ShouldBeNull();
        position.Value!.Value.ShouldBe(new Money(90m, Usd));
    }

    [Fact]
    public void Valuate_ShouldConvertEuropeanPositionsWhenTheReferenceCurrencyIsForeign()
    {
        var usdPortfolio = new InvestmentPortfolio(
            Guid.Parse("5b1f5a55-3a5b-4a3f-ac11-2bcbcb9ce6cb"), "Portafoglio USA", Usd);
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            usdPortfolio,
            Etf,
            EuroAccount,
            new Quantity(1m),
            Quote(100m, Euro),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);
        var priceInEur = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(100m, Euro)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");
        var rate = ExchangeRateObservation(1.10m, new DateOnly(2026, 3, 15));

        var valuation = PortfolioValuationCalculator.Valuate(
            usdPortfolio, [operation], [priceInEur], [rate], ValuationDate);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.ExchangeRateObservation.ShouldBe(rate);
        position.Value!.Value.ShouldBe(new Money(110m, Usd));
    }

    [Fact]
    public void Valuate_ShouldBeIndependentFromTheInputOrder()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(70m, Euro)), new DateOnly(2026, 3, 5), RecordedAt, "STOOQ");
        var currentPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 18), RecordedAt, "Manuale");

        var forward = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [olderPrice, currentPrice], [], ValuationDate);
        var backward = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [currentPrice, olderPrice], [], ValuationDate);

        backward.Positions[0].PriceObservation.ShouldBe(currentPrice);
        backward.Positions[0].Value!.Value.ShouldBe(new Money(200m, Euro));
        forward.Positions[0].PriceObservation.ShouldBe(currentPrice);
        forward.Positions[0].Value!.Value.ShouldBe(new Money(200m, Euro));
    }

    [Fact]
    public void Valuate_ShouldNotMutateTheProvidedCollections()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 10));
        var olderPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(70m, Euro)), new DateOnly(2026, 3, 5), RecordedAt, "STOOQ");
        var currentPrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 18), RecordedAt, "Manuale");
        var priceObservations = new List<PriceObservation> { currentPrice, olderPrice };
        var exchangeRateObservations = new List<ExchangeRateObservation>();

        PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], priceObservations, exchangeRateObservations, ValuationDate);

        priceObservations.Count.ShouldBe(2);
        exchangeRateObservations.ShouldBeEmpty();
    }

    [Fact]
    public void Valuate_ShouldRejectPortsWhoseReferenceCurrencyCannotBeBridgedWithEuro()
    {
        var gbp = Currency.Parse("GBP");
        var gbpPortfolio = new InvestmentPortfolio(
            Guid.Parse("5b1f5a55-3a5b-4a3f-ac11-2bcbcb9ce6cb"), "Portafoglio sterlina", gbp);
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            gbpPortfolio,
            UsdInstrument,
            UsdAccount,
            new Quantity(1m),
            Quote(80m, Usd),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(90m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");
        var rate = ExchangeRateObservation(1.10m, new DateOnly(2026, 3, 15));

        var exception = Should.Throw<InvalidOperationException>(() => PortfolioValuationCalculator.Valuate(
            gbpPortfolio, [operation], [priceInUsd], [rate], ValuationDate));

        exception.Message.ShouldContain("non è supportata");
        exception.Message.ShouldContain("§4.3");
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionIncompleteWhenTheLatestPriceIsStale()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var stalePrice = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 1), RecordedAt, "STOOQ");
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [stalePrice], [], ValuationDate, policy);

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
        var priceAtLimit = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 21), RecordedAt, "STOOQ");
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceAtLimit], [], ValuationDate, policy);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsValuated.ShouldBeTrue();
        position.Value!.Value.ShouldBe(new Money(200m, Euro));
    }

    [Fact]
    public void Valuate_ShouldMarkThePositionIncompleteWhenTheExchangeRateIsStale()
    {
        var operation = Buy(UsdInstrument, 5m, new DateOnly(2026, 3, 10));
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(22m, Usd)), new DateOnly(2026, 3, 15), RecordedAt, "STOOQ");
        var staleRate = ExchangeRateObservation(1.10m, new DateOnly(2026, 3, 1));
        var policy = new ValuationPolicy(maximumExchangeRateAgeDays: 10);

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [priceInUsd], [staleRate], ValuationDate, policy);

        var position = valuation.Positions.ShouldHaveSingleItem();
        position.IsIncomplete.ShouldBeTrue();
        position.Value.ShouldBeNull();
        position.PriceObservation.ShouldBe(priceInUsd);
        position.ExchangeRateObservation.ShouldBe(staleRate);
        position.IncompletenessReason.ShouldNotBeNull();
        position.IncompletenessReason!.ShouldContain("obsoleto");
        position.IncompletenessReason.ShouldContain("soglia 10");
    }

    [Fact]
    public void Valuate_ShouldUseIndependentPriceAndExchangeRateThresholds()
    {
        var operation = Buy(UsdInstrument, 5m, new DateOnly(2026, 3, 10));
        var priceInUsd = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), UsdInstrument, new UnitPrice(new Money(22m, Usd)), new DateOnly(2026, 3, 25), RecordedAt, "STOOQ");
        var rate = ExchangeRateObservation(1.10m, new DateOnly(2026, 3, 1));

        var accepted = PortfolioValuationCalculator.Valuate(
            Portfolio,
            [operation],
            [priceInUsd],
            [rate],
            ValuationDate,
            new ValuationPolicy(maximumPriceAgeDays: 7, maximumExchangeRateAgeDays: 60));

        accepted.Positions.ShouldHaveSingleItem().Value!.Value.ShouldBe(new Money(100m, Euro));

        var rejected = PortfolioValuationCalculator.Valuate(
            Portfolio,
            [operation],
            [priceInUsd],
            [rate],
            ValuationDate,
            new ValuationPolicy(maximumPriceAgeDays: 7, maximumExchangeRateAgeDays: 10));

        rejected.Positions.ShouldHaveSingleItem().IncompletenessReason!.ShouldContain("obsoleto");
    }

    [Fact]
    public void Valuate_ShouldKeepTheLatestObservedQuoteBeforeCheckingStaleness()
    {
        var operation = Buy(Etf, 2.5m, new DateOnly(2026, 3, 1));
        var earlier = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(80m, Euro)), new DateOnly(2026, 3, 1), RecordedAt, "STOOQ");
        var later = PriceObservation.AtUnitPrice(
            Guid.NewGuid(), Etf, new UnitPrice(new Money(90m, Euro)), new DateOnly(2026, 3, 1), RecordedAt.AddHours(3), "Manuale");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio,
            [operation],
            [earlier, later],
            [],
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
            EuroAccount,
            new Quantity(3m),
            QuotedPrice.AsPercentOfNominal(98.5m),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);
        var price = PriceObservation.AsPercentOfNominal(
            Guid.NewGuid(), Bond, 98.5m, new DateOnly(2026, 3, 15), RecordedAt, "Mercato obbligazionario");

        var valuation = PortfolioValuationCalculator.Valuate(
            Portfolio, [operation], [price], [], ValuationDate);

        var positionValue = valuation.Positions.ShouldHaveSingleItem().Value!.Value;
        positionValue.ShouldBe(new Money(2955m, Euro));
        var capital = operation.GeneratePostings().Single(posting => posting.Kind == PostingKind.Capital);
        capital.SignedAmount.ShouldBe(-positionValue.Amount);
    }

    [Fact]
    public void Valuate_ShouldRejectNullCollections()
    {
        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(null!, [], [], [], ValuationDate))
            .ParamName.ShouldBe("portfolio");

        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(Portfolio, null!, [], [], ValuationDate))
            .ParamName.ShouldBe("operations");

        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(Portfolio, [], null!, [], ValuationDate))
            .ParamName.ShouldBe("priceObservations");

        Should.Throw<ArgumentNullException>(
                () => PortfolioValuationCalculator.Valuate(Portfolio, [], [], null!, ValuationDate))
            .ParamName.ShouldBe("exchangeRateObservations");
    }

    private static InvestmentOperation Buy(Instrument instrument, decimal quantity, DateOnly date) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            Portfolio,
            instrument,
            instrument.QuoteCurrency == Euro ? EuroAccount : UsdAccount,
            new Quantity(quantity),
            instrument.PriceUnit == PriceUnit.PercentOfNominal
                ? QuotedPrice.AsPercentOfNominal(98.5m)
                : Quote(80m, instrument.QuoteCurrency),
            null,
            date,
            RecordedAt);

    private static QuotedPrice Quote(decimal price, Currency currency) =>
        QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, currency)));

    private static ExchangeRateObservation ExchangeRateObservation(decimal rate, DateOnly validAt) => new(
        Guid.NewGuid(), new ExchangeRate(Usd, rate), validAt, RecordedAt, "ECB");
}
