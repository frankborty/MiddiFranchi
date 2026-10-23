using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class NetWorthCalculatorTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Category IncomeCategory = new(
        Guid.Parse("f8f5d4f1-6a2b-4f4b-9d8d-3f6d0b1a2c3d"), "Stipendio", CategoryType.Income);
    private static readonly Instrument Etf = new(
        Guid.Parse("b1f6c1a9-d3a4-4c5e-9f2a-6c6b5d0e1a2b"), "ETF globale", AssetClass.Equity, Euro, PriceUnit.PerUnit);

    [Fact]
    public void Summarize_ShouldComputeAccountsAndInvestmentsInEur()
    {
        var reading = new ManualBalance(
            Guid.NewGuid(), CheckingAccount, new Money(1000m, Euro), ValuationDate, RecordedAt);
        var buy = Buy(2m, 80m);
        var price = Price(90m);

        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount],
            [],
            [buy],
            [price],
            ValuationDate,
            ValuationPolicy.Unbounded,
            [reading]);

        summary.IsComplete.ShouldBeTrue();
        summary.AccountsSubtotal.ShouldBe(new Money(1000m, Euro));
        summary.InvestmentsSubtotal.ShouldBe(new Money(180m, Euro));
        summary.TotalValue.ShouldBe(new Money(1180m, Euro));
        summary.Accounts.ShouldHaveSingleItem().Balance.ShouldBe(new Money(1000m, Euro));
        summary.Investments.Positions.ShouldHaveSingleItem()
            .Value!.Value.ShouldBe(new Money(180m, Euro));
    }

    [Fact]
    public void Summarize_ShouldUsePostingsWhenManualBalancesAreOmitted()
    {
        var income = new Income(
            Guid.NewGuid(),
            CheckingAccount,
            IncomeCategory,
            new PositiveMoney(new Money(100m, Euro)),
            new DateOnly(2026, 3, 10),
            RecordedAt);

        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount],
            income.GeneratePostings(),
            [],
            [],
            ValuationDate);

        summary.IsComplete.ShouldBeTrue();
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.TotalValue.ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Summarize_ShouldMarkAnAccountWithoutManualReadingIncomplete()
    {
        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount],
            [],
            [],
            [],
            ValuationDate,
            ValuationPolicy.Unbounded,
            []);

        summary.IsComplete.ShouldBeFalse();
        summary.AccountsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("rilevazione manuale"));
    }

    [Fact]
    public void Summarize_ShouldMarkAnUnvaluedPositionIncomplete()
    {
        var buy = Buy(2m, 80m);

        var summary = NetWorthCalculator.Summarize(
            [],
            [],
            [buy],
            [],
            ValuationDate);

        summary.IsComplete.ShouldBeFalse();
        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("quotazione"));
    }

    [Fact]
    public void Summarize_ShouldRejectDuplicateAccounts()
    {
        Should.Throw<ArgumentException>(
                () => NetWorthCalculator.Summarize(
                    [CheckingAccount, CheckingAccount], [], [], [], ValuationDate))
            .Message.ShouldContain("presente più volte");
    }

    [Fact]
    public void Summarize_ShouldRejectNullCollections()
    {
        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize(null!, [], [], [], ValuationDate))
            .ParamName.ShouldBe("accounts");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], null!, [], [], ValuationDate))
            .ParamName.ShouldBe("postings");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], null!, [], ValuationDate))
            .ParamName.ShouldBe("operations");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], [], null!, ValuationDate))
            .ParamName.ShouldBe("priceObservations");
    }

    private static InvestmentOperation Buy(decimal quantity, decimal price) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            Etf,
            CheckingAccount,
            new Quantity(quantity),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, Euro))),
            null,
            new DateOnly(2026, 3, 10),
            RecordedAt);

    private static PriceObservation Price(decimal price) =>
        PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            Etf,
            new UnitPrice(new Money(price, Euro)),
            new DateOnly(2026, 3, 15),
            RecordedAt,
            "Test");
}
