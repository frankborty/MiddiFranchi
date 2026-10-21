using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class InvestmentOperationTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly InvestmentPortfolio MainPortfolio = new(
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
    private static readonly Account SavingsAccount = new(
        Guid.Parse("6c7f1e09-8f2d-4c6b-bd0e-c8a4c8402f9c"), "Risparmio", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account UsdAccount = new(
        Guid.Parse("9ae1be54-3eef-4971-9ad4-d58ca44dd7cb"), "Conto valute", AccountType.Checking, Usd, AccountStatus.Active);
    private static readonly Guid OperationId = Guid.Parse("b66e1721-8dc2-4edc-a9be-3a05a89b2d18");
    private static readonly DateOnly OperationDate = new(2026, 3, 18);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Buy_ShouldExposeTheInvestmentOperationFields()
    {
        var operation = InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            [BuyFee(1.50m)],
            OperationDate,
            RecordedAt,
            "Acquisto ETF",
            null);

        operation.Kind.ShouldBe(OperationKind.Investment);
        operation.InvestmentKind.ShouldBe(InvestmentOperationKind.Buy);
        operation.Portfolio.ShouldBe(MainPortfolio);
        operation.Instrument.ShouldBe(Etf);
        operation.SettlementAccount.ShouldBe(EuroAccount);
        operation.Quantity.ShouldNotBeNull();
        operation.Quantity!.Value.Value.ShouldBe(2.5m);
        operation.UnitPrice.ShouldNotBeNull();
        operation.UnitPrice!.Value.Price.ShouldBe(new Money(80m, Euro));
        operation.QuotedPrice.ShouldBe(QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Euro))));
        operation.Amount.ShouldBeNull();
        operation.Fees.Count.ShouldBe(1);
        operation.OperationDate.ShouldBe(OperationDate);
        operation.RecordedAt.ShouldBe(RecordedAt);
    }

    [Fact]
    public void Buy_ShouldGenerateACapitalPostingAndAFeePosting()
    {
        var operation = InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            [BuyFee(1.50m)],
            OperationDate,
            RecordedAt);

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(2);
        var capital = postings[0];
        capital.Kind.ShouldBe(PostingKind.Capital);
        capital.SignedAmount.ShouldBe(-200m);
        capital.Currency.ShouldBe(Euro);
        capital.AccountId.ShouldBe(EuroAccount.Id);
        var fee = postings[1];
        fee.Kind.ShouldBe(PostingKind.Fee);
        fee.SignedAmount.ShouldBe(-1.50m);
        fee.Currency.ShouldBe(Euro);
        postings.ShouldAllBe(posting => posting.OperationId == OperationId);
        postings.ShouldAllBe(posting => posting.OperationDate == OperationDate);
    }

    [Fact]
    public void Sell_ShouldGenerateACapitalPostingAndAFeePosting()
    {
        var operation = InvestmentOperation.Sell(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(1m),
            Quote(95m, Euro),
            [SellFee(1m)],
            OperationDate,
            RecordedAt);

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(2);
        postings[0].Kind.ShouldBe(PostingKind.Capital);
        postings[0].SignedAmount.ShouldBe(95m);
        postings[1].Kind.ShouldBe(PostingKind.Fee);
        postings[1].SignedAmount.ShouldBe(-1m);
    }

    [Fact]
    public void Dividend_ShouldGenerateASingleIncomePosting()
    {
        var operation = InvestmentOperation.Dividend(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            Eur(10m),
            OperationDate,
            RecordedAt);

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(1);
        postings[0].Kind.ShouldBe(PostingKind.Income);
        postings[0].SignedAmount.ShouldBe(10m);
        postings[0].Currency.ShouldBe(Euro);
    }

    [Fact]
    public void Coupon_ShouldGenerateASingleIncomePostingOnABondQuotedAsPercentOfNominal()
    {
        var operation = InvestmentOperation.Coupon(
            OperationId,
            MainPortfolio,
            Bond,
            EuroAccount,
            Eur(25m),
            OperationDate,
            RecordedAt);

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(1);
        postings[0].Kind.ShouldBe(PostingKind.Income);
        postings[0].SignedAmount.ShouldBe(25m);
        postings[0].Currency.ShouldBe(Euro);
    }

    [Fact]
    public void StandaloneFee_ShouldGenerateASingleFeePostingWithoutAnInstrument()
    {
        var operation = InvestmentOperation.StandaloneFee(
            OperationId,
            MainPortfolio,
            EuroAccount,
            Eur(0.50m),
            OperationDate,
            RecordedAt);

        operation.InvestmentKind.ShouldBe(InvestmentOperationKind.StandaloneFee);
        operation.Instrument.ShouldBeNull();
        operation.Fees.Count.ShouldBe(1);
        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(1);
        postings[0].Kind.ShouldBe(PostingKind.Fee);
        postings[0].SignedAmount.ShouldBe(-0.50m);
        postings[0].Currency.ShouldBe(Euro);
    }

    [Fact]
    public void StandaloneFee_ShouldAcceptAnOptionalInstrument()
    {
        var operation = InvestmentOperation.StandaloneFee(
            OperationId,
            MainPortfolio,
            EuroAccount,
            Eur(0.50m),
            OperationDate,
            RecordedAt,
            Etf);

        operation.InvestmentKind.ShouldBe(InvestmentOperationKind.StandaloneFee);
        operation.Instrument.ShouldBe(Etf);
        operation.GeneratePostings().Count.ShouldBe(1);
    }

    [Fact]
    public void Buy_ShouldRequireAnInstrument()
    {
        var exception = Should.Throw<ArgumentException>(
            () => InvestmentOperation.Buy(
                OperationId,
                MainPortfolio,
                null!,
                EuroAccount,
                new Quantity(2.5m),
                Quote(80m, Euro),
                null,
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("instrument");
        exception.Message.ShouldContain("strumento finanziario");
    }



    [Fact]
    public void Buy_ShouldRequireFeesWithTheBuyFeeKind()
    {
        var exception = Should.Throw<ArgumentException>(
            () => InvestmentOperation.Buy(
                OperationId,
                MainPortfolio,
                Etf,
                EuroAccount,
                new Quantity(2.5m),
                Quote(80m, Euro),
                [SellFee(1.50m)],
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("fees");
        exception.Message.ShouldContain("devono essere di tipo BuyFee");
    }

    [Fact]
    public void Sell_ShouldRequireFeesWithTheSellFeeKind()
    {
        var exception = Should.Throw<ArgumentException>(
            () => InvestmentOperation.Sell(
                OperationId,
                MainPortfolio,
                Etf,
                EuroAccount,
                new Quantity(1m),
                Quote(95m, Euro),
                [BuyFee(1m)],
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("fees");
        exception.Message.ShouldContain("devono essere di tipo SellFee");
    }

    [Fact]
    public void Buy_ShouldRequireFeesDebitedToTheSettlementAccount()
    {
        var exception = Should.Throw<ArgumentException>(
            () => InvestmentOperation.Buy(
                OperationId,
                MainPortfolio,
                Etf,
                EuroAccount,
                new Quantity(2.5m),
                Quote(80m, Euro),
                [BuyFeeOn(SavingsAccount, 1.50m)],
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("fees");
        exception.Message.ShouldContain("conto di regolamento");
    }


    [Fact]
    public void Dividend_ShouldRequireAnInstrument()
    {
        var exception = Should.Throw<ArgumentException>(
            () => InvestmentOperation.Dividend(
                OperationId,
                MainPortfolio,
                null!,
                EuroAccount,
                Eur(10m),
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("instrument");
        exception.Message.ShouldContain("strumento finanziario");
    }

    [Fact]
    public void Construction_ShouldRejectANullPortfolio()
    {
        Should.Throw<ArgumentNullException>(() => InvestmentOperation.Buy(
            OperationId,
            null!,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            null,
            OperationDate,
            RecordedAt));
    }

    [Fact]
    public void Construction_ShouldRejectANullSettlementAccount()
    {
        Should.Throw<ArgumentNullException>(() => InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Etf,
            null!,
            new Quantity(2.5m),
            Quote(80m, Euro),
            null,
            OperationDate,
            RecordedAt));
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyOperationIdentifier()
    {
        Should.Throw<ArgumentException>(() => InvestmentOperation.Buy(
            Guid.Empty,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            null,
            OperationDate,
            RecordedAt))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Balance_ShouldIntegrateBuyPostings()
    {
        var operation = InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            [BuyFee(1.50m)],
            OperationDate,
            RecordedAt);

        AccountBalanceCalculator
            .Balance(EuroAccount, operation.GeneratePostings(), OperationDate)
            .ShouldBe(new Money(-201.50m, Euro));
    }

    [Fact]
    public void Balance_ShouldIntegrateSellPostings()
    {
        var operation = InvestmentOperation.Sell(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(1m),
            Quote(95m, Euro),
            [SellFee(1m)],
            OperationDate,
            RecordedAt);

        AccountBalanceCalculator
            .Balance(EuroAccount, operation.GeneratePostings(), OperationDate)
            .ShouldBe(new Money(94m, Euro));
    }

    [Fact]
    public void Buy_OnBondQuotedAsPercentOfNominal_ShouldUseTheEffectiveUnitPrice()
    {
        var operation = InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Bond,
            EuroAccount,
            new Quantity(3m),
            QuotedPrice.AsPercentOfNominal(98.5m),
            [BuyFee(2m)],
            OperationDate,
            RecordedAt);

        operation.QuotedPrice.ShouldBe(QuotedPrice.AsPercentOfNominal(98.5m));
        operation.UnitPrice!.Value.Price.ShouldBe(new Money(985m, Euro));

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(2);
        postings[0].Kind.ShouldBe(PostingKind.Capital);
        postings[0].SignedAmount.ShouldBe(-2955m);
        postings[1].Kind.ShouldBe(PostingKind.Fee);
        postings[1].SignedAmount.ShouldBe(-2m);
        AccountBalanceCalculator
            .Balance(EuroAccount, postings, OperationDate)
            .ShouldBe(new Money(-2957m, Euro));
    }

    [Fact]
    public void Sell_OnBondQuotedAsPercentOfNominal_ShouldUseTheEffectiveUnitPrice()
    {
        var operation = InvestmentOperation.Sell(
            OperationId,
            MainPortfolio,
            Bond,
            EuroAccount,
            new Quantity(3m),
            QuotedPrice.AsPercentOfNominal(98.5m),
            [SellFee(2m)],
            OperationDate,
            RecordedAt);

        operation.UnitPrice!.Value.Price.ShouldBe(new Money(985m, Euro));

        var postings = operation.GeneratePostings();

        postings.Count.ShouldBe(2);
        postings[0].Kind.ShouldBe(PostingKind.Capital);
        postings[0].SignedAmount.ShouldBe(2955m);
        postings[1].Kind.ShouldBe(PostingKind.Fee);
        postings[1].SignedAmount.ShouldBe(-2m);
        AccountBalanceCalculator
            .Balance(EuroAccount, postings, OperationDate)
            .ShouldBe(new Money(2953m, Euro));
    }

    [Fact]
    public void Buy_ShouldRejectADefaultQuantity()
    {
        Should.Throw<ArgumentException>(
                () => InvestmentOperation.Buy(
                    OperationId,
                    MainPortfolio,
                    Etf,
                    EuroAccount,
                    default,
                    Quote(80m, Euro),
                    null,
                    OperationDate,
                    RecordedAt))
            .ParamName.ShouldBe("quantity");
    }

    [Fact]
    public void Buy_ShouldRejectAQuoteUnitThatDoesNotMatchTheInstrument()
    {
        Should.Throw<ArgumentException>(
                () => InvestmentOperation.Buy(
                    OperationId,
                    MainPortfolio,
                    Etf,
                    EuroAccount,
                    new Quantity(1m),
                    QuotedPrice.AsPercentOfNominal(98.5m),
                    null,
                    OperationDate,
                    RecordedAt))
            .ParamName.ShouldBe("quotedPrice");

        Should.Throw<ArgumentException>(
                () => InvestmentOperation.Buy(
                    OperationId,
                    MainPortfolio,
                    Bond,
                    EuroAccount,
                    new Quantity(1m),
                    Quote(985m, Euro),
                    null,
                    OperationDate,
                    RecordedAt))
            .ParamName.ShouldBe("quotedPrice");
    }

    [Fact]
    public void Buy_ShouldNotRetainTheFeesCollection()
    {
        var fees = new List<FeeComponent> { BuyFee(1.50m) };
        var operation = InvestmentOperation.Buy(
            OperationId,
            MainPortfolio,
            Etf,
            EuroAccount,
            new Quantity(2.5m),
            Quote(80m, Euro),
            fees,
            OperationDate,
            RecordedAt);

        fees.Clear();

        operation.Fees.Count.ShouldBe(1);
    }

    private static QuotedPrice Quote(decimal price, Currency currency) =>
        QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, currency)));

    private static FeeComponent BuyFee(decimal amount) => new(
        FeeKind.BuyFee,
        new PositiveMoney(new Money(amount, Euro)),
        EuroAccount);

    private static FeeComponent SellFee(decimal amount) => new(
        FeeKind.SellFee,
        new PositiveMoney(new Money(amount, Euro)),
        EuroAccount);

    private static FeeComponent BuyFeeOn(Account account, decimal amount) => new(
        FeeKind.BuyFee,
        new PositiveMoney(new Money(amount, account.Currency)),
        account);

    private static PositiveMoney Eur(decimal amount) => new(new Money(amount, Euro));

    private static PositiveMoney UsdAmount(decimal amount) => new(new Money(amount, Usd));
}
