using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class FinancialDashboardServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_ShouldExcludeTransfersFromMonthTotalsButCountThemAsMovements()
    {
        var project = new DashboardProject();
        var checking = project.AddAccount("Conto corrente", Euro);
        var savings = project.AddAccount("Risparmio", Euro);
        project.AddReading(checking, 500m);
        project.AddReading(savings, 100m);
        project.AddIncome(checking, 1000m, new DateOnly(2026, 10, 5));
        project.AddExpense(checking, 200m, new DateOnly(2026, 10, 6));
        project.AddTransfer(checking, savings, 50m, 50m, new DateOnly(2026, 10, 7));

        var dashboard = await project.Build().LoadAsync();

        var flow = dashboard.Month.Flows.ShouldHaveSingleItem();
        flow.Currency.ShouldBe(Euro);
        flow.Income.ShouldBe(1000m);
        flow.Expense.ShouldBe(200m);
        flow.Net.ShouldBe(800m);
        flow.OperationCount.ShouldBe(2);
        dashboard.Month.MovementCount.ShouldBe(3);
        dashboard.Month.IncomeExpenseOperationCount.ShouldBe(2);
        dashboard.Month.OtherOperationCount.ShouldBe(1);
        dashboard.NetWorth.TotalValue.ShouldBe(new Money(600m, Euro));

        dashboard.RecentMovements.Count.ShouldBe(3);
        var transfer = dashboard.RecentMovements.Single(movement =>
            movement.OperationKind == OperationKind.Transfer);
        transfer.Amount.ShouldBe(new Money(-50m, Euro));
        transfer.CounterAmount.ShouldBe(new Money(50m, Euro));
        transfer.AccountLabel.ShouldContain(checking.Name);
        transfer.AccountLabel.ShouldContain(savings.Name);
    }

    [Fact]
    public async Task LoadAsync_ShouldLeaveTheMonthEmptyWhenThereAreNoMovements()
    {
        var project = new DashboardProject();
        var account = project.AddAccount("Conto corrente", Euro);
        project.AddReading(account, 100m);

        var dashboard = await project.Build().LoadAsync();

        dashboard.Month.PeriodStart.ShouldBe(MonthStart);
        dashboard.Month.PeriodEnd.ShouldBe(Today);
        dashboard.Month.HasMovements.ShouldBeFalse();
        dashboard.Month.HasIncomeExpense.ShouldBeFalse();
        dashboard.Month.Flows.ShouldBeEmpty();
        dashboard.RecentMovements.ShouldBeEmpty();
        dashboard.NetWorth.TotalValue.ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public async Task LoadAsync_ShouldNotTreatAnUnvaluedPositionAsZero()
    {
        var project = new DashboardProject();
        var account = project.AddAccount("Conto titoli", Euro);
        project.AddReading(account, 100m);
        var portfolio = project.AddPortfolio("Portafoglio", Euro);
        var etf = project.AddInstrument("ETF globale", Euro);
        project.AddBuy(portfolio, etf, account, 2m, 80m, new DateOnly(2026, 10, 5));

        var dashboard = await project.Build().LoadAsync();

        dashboard.NetWorth.IsComplete.ShouldBeFalse();
        dashboard.NetWorth.TotalValue.ShouldBeNull();
        dashboard.Investments.Total.ShouldBeNull();
        dashboard.Investments.HasValuedPositions.ShouldBeFalse();
        dashboard.Investments.ValuedPositionCount.ShouldBe(0);
        dashboard.Investments.UnvaluedPositionCount.ShouldBe(1);
        var valuation = dashboard.Investments.Portfolios.ShouldHaveSingleItem();
        valuation.Total.ShouldBeNull();
        valuation.Distribution.ShouldBeEmpty();
        valuation.Positions.ShouldHaveSingleItem().Value.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldBuildACompleteNetWorthAndARealDistribution()
    {
        var project = new DashboardProject();
        var account = project.AddAccount("Conto titoli", Euro);
        project.AddReading(account, 100m);
        var portfolio = project.AddPortfolio("Portafoglio", Euro);
        var etf = project.AddInstrument("ETF globale", Euro);
        project.AddBuy(portfolio, etf, account, 2.5m, 80m, new DateOnly(2026, 10, 5));
        project.AddPrice(etf, 90m, new DateOnly(2026, 10, 15));

        var dashboard = await project.Build().LoadAsync();

        dashboard.NetWorth.IsComplete.ShouldBeTrue();
        dashboard.NetWorth.TotalValue.ShouldBe(new Money(325m, Euro));
        dashboard.NetWorth.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        dashboard.NetWorth.InvestmentsSubtotal.ShouldBe(new Money(225m, Euro));
        dashboard.Investments.Total.ShouldBe(new Money(225m, Euro));
        dashboard.Investments.ValuedPositionCount.ShouldBe(1);
        dashboard.Investments.UnvaluedPositionCount.ShouldBe(0);
        var valuation = dashboard.Investments.Portfolios.ShouldHaveSingleItem();
        valuation.Total.ShouldBe(new Money(225m, Euro));
        valuation.Distribution.ShouldHaveSingleItem().Share.ShouldBe(1d);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportAnAccountWithoutAManualReadingAsUnavailable()
    {
        var project = new DashboardProject();
        var withoutReading = project.AddAccount("Cassa senza rilevazione", Euro);
        var withReading = project.AddAccount("Conto corrente", Euro);
        project.AddReading(withReading, 100m);

        var dashboard = await project.Build().LoadAsync();

        dashboard.Accounts.Single(account => account.AccountId == withoutReading.Id)
            .ManualBalance.ShouldBeNull();
        dashboard.Accounts.Single(account => account.AccountId == withReading.Id)
            .ManualBalance.ShouldBe(new Money(100m, Euro));
        dashboard.NetWorth.IsComplete.ShouldBeFalse();
        dashboard.NetWorth.TotalValue.ShouldBeNull();
        dashboard.NetWorth.IncompletenessReasons.ShouldContain(reason =>
            reason.Contains(withoutReading.Name));
    }

    private sealed class DashboardProject
    {
        private readonly FakeAccountPostingRepository _postings;

        public DashboardProject()
        {
            _postings = new FakeAccountPostingRepository(Operations.Operations);
        }

        public FakeAccountRepository Accounts { get; } = new();

        public FakeManualBalanceRepository Readings { get; } = new();

        public FakeFinancialOperationRepository Operations { get; } = new();

        public FakeInvestmentPortfolioRepository Portfolios { get; } = new();

        public FakePriceObservationRepository Prices { get; } = new();

        public FinancialDashboardService Build() => new(
            new NetWorthOverviewService(
                Accounts,
                Readings,
                Portfolios,
                Operations,
                Prices),
            new AccountOverviewService(Accounts, Readings, _postings),
            Operations,
            () => Today);

        public Account AddAccount(string name, Currency currency)
        {
            var account = new Account(
                Guid.NewGuid(), name, AccountType.Checking, currency, AccountStatus.Active);
            Accounts.Accounts.Add(account);
            return account;
        }

        public void AddReading(Account account, decimal amount) =>
            Readings.Readings.Add(new ManualBalance(
                Guid.NewGuid(),
                account,
                new Money(amount, account.Currency),
                MonthStart,
                RecordedAt));

        public Income AddIncome(Account account, decimal amount, DateOnly date)
        {
            var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
            var income = new Income(
                Guid.NewGuid(),
                account,
                category,
                new PositiveMoney(new Money(amount, account.Currency)),
                date,
                RecordedAt);
            Operations.Operations.Add(income);
            return income;
        }

        public Expense AddExpense(Account account, decimal amount, DateOnly date)
        {
            var category = new Category(Guid.NewGuid(), "Spese", CategoryType.Expense);
            var expense = new Expense(
                Guid.NewGuid(),
                account,
                category,
                new PositiveMoney(new Money(amount, account.Currency)),
                date,
                RecordedAt);
            Operations.Operations.Add(expense);
            return expense;
        }

        public Transfer AddTransfer(
            Account source,
            Account destination,
            decimal sourceAmount,
            decimal destinationAmount,
            DateOnly date)
        {
            var transfer = new Transfer(
                Guid.NewGuid(),
                source,
                destination,
                new PositiveMoney(new Money(sourceAmount, source.Currency)),
                new PositiveMoney(new Money(destinationAmount, destination.Currency)),
                date,
                RecordedAt);
            Operations.Operations.Add(transfer);
            return transfer;
        }

        public InvestmentPortfolio AddPortfolio(string name, Currency currency)
        {
            var portfolio = new InvestmentPortfolio(Guid.NewGuid(), name, currency);
            Portfolios.Portfolios.Add(portfolio);
            return portfolio;
        }

        public Instrument AddInstrument(string name, Currency currency)
        {
            var instrument = new Instrument(
                Guid.NewGuid(), name, InstrumentType.Etf, currency, PriceUnit.PerUnit);
            return instrument;
        }

        public InvestmentOperation AddBuy(
            InvestmentPortfolio portfolio,
            Instrument instrument,
            Account settlementAccount,
            decimal quantity,
            decimal unitPrice,
            DateOnly date)
        {
            var operation = InvestmentOperation.Buy(
                Guid.NewGuid(),
                portfolio,
                instrument,
                settlementAccount,
                new Quantity(quantity),
                QuotedPrice.AtUnitPrice(new UnitPrice(new Money(unitPrice, instrument.QuoteCurrency))),
                null,
                date,
                RecordedAt);
            Operations.Operations.Add(operation);
            return operation;
        }

        public PriceObservation AddPrice(Instrument instrument, decimal price, DateOnly validAt)
        {
            var observation = PriceObservation.AtUnitPrice(
                Guid.NewGuid(),
                instrument,
                new UnitPrice(new Money(price, instrument.QuoteCurrency)),
                validAt,
                RecordedAt,
                "Test");
            Prices.Observations.Add(observation);
            return observation;
        }
    }
}
