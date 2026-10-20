using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class NetWorthCalculatorTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Currency Gbp = Currency.Parse("GBP");
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Category Groceries = new(
        Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668"), "Generica", CategoryType.Income);

    [Fact]
    public void Summary_ShouldReturnTheBalanceOfASingleAccount()
    {
        var checking = CheckingAccount();
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [checking], postings, [], [], [], [], ValuationDate);

        summary.ReportingCurrency.ShouldBe(Euro);
        summary.IsComplete.ShouldBeTrue();
        summary.Accounts.ShouldHaveSingleItem().Balance.ShouldBe(new Money(100m, Euro));
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.InvestmentsSubtotal.ShouldBe(new Money(0m, Euro));
        summary.TotalValue.ShouldBe(new Money(100m, Euro));
        summary.IncompletenessReasons.ShouldBeEmpty();
    }

    [Fact]
    public void Summary_ShouldSumMultipleAccountsInTheSameCurrency()
    {
        var checking = CheckingAccount();
        var savings = Account("Risparmio", Euro);
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10))
            .Concat(Incomes(savings, 50m, new DateOnly(2026, 3, 11)))
            .ToArray();

        var summary = NetWorthCalculator.Summarize(
            [checking, savings], postings, [], [], [], [], ValuationDate);

        summary.AccountsSubtotal.ShouldBe(new Money(150m, Euro));
        summary.TotalValue.ShouldBe(new Money(150m, Euro));
        summary.IncompletenessReasons.ShouldBeEmpty();
    }

    [Fact]
    public void Summary_ShouldUseNegativeBalancesOfLiabilityAccounts()
    {
        var checking = CheckingAccount();
        var card = Account("Carta di credito", Euro, AccountType.Card);
        var postings = Incomes(checking, 120m, new DateOnly(2026, 3, 10))
            .Concat(Expenses(card, 80m, new DateOnly(2026, 3, 12)))
            .ToArray();

        var summary = NetWorthCalculator.Summarize(
            [checking, card], postings, [], [], [], [], ValuationDate);

        summary.AccountsSubtotal.ShouldBe(new Money(40m, Euro));
        summary.TotalValue.ShouldBe(new Money(40m, Euro));
    }

    [Fact]
    public void Summary_ShouldConvertForeignAccountsWithTheChangeOfValuation()
    {
        var checking = CheckingAccount();
        var usdAccount = Account("Conto valute", Usd);
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10))
            .Concat(Incomes(usdAccount, 55m, new DateOnly(2026, 3, 10)))
            .ToArray();
        var usdRate = UsdObservation(1.10m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [checking, usdAccount], postings, [], [], [], [usdRate], ValuationDate);

        summary.Accounts.Count.ShouldBe(2);
        summary.AccountsSubtotal.ShouldBe(new Money(150m, Euro));
        summary.InvestmentsSubtotal.ShouldBe(new Money(0m, Euro));
        summary.TotalValue.ShouldBe(new Money(150m, Euro));
        summary.IncompletenessReasons.ShouldBeEmpty();
    }

    [Fact]
    public void Summary_ShouldMarkIncompleteAnAccountWithoutAChangeOfValuation()
    {
        var usdAccount = Account("Conto valute", Usd);
        var postings = Incomes(usdAccount, 55m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [usdAccount], postings, [], [], [], [], ValuationDate);

        summary.IsComplete.ShouldBeFalse();
        summary.Accounts.ShouldHaveSingleItem().ConvertedBalance.ShouldBeNull();
        summary.AccountsSubtotal.ShouldBeNull();
        summary.InvestmentsSubtotal.ShouldBe(new Money(0m, Euro));
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldHaveSingleItem().ShouldContain("nessuna osservazione valida");
    }

    [Fact]
    public void Summary_ShouldCombineBalancesAndInvestmentValuations()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Euro);
        var etf = Etf(PriceUnit.PerUnit);
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10));
        var buy = Buy(etf, 2.5m, portfolio, 80m, new DateOnly(2026, 3, 10));
        var price = Price(etf, 90m, new DateOnly(2026, 3, 20), asPercent: false);

        var summary = NetWorthCalculator.Summarize(
            [checking], postings, [portfolio], [buy], [price], [], ValuationDate);

        summary.Investments.ShouldHaveSingleItem().Portfolio.ShouldBe(portfolio);
        summary.InvestmentsSubtotal.ShouldBe(new Money(225m, Euro));
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.TotalValue.ShouldBe(new Money(325m, Euro));
        summary.IsComplete.ShouldBeTrue();
        summary.IncompletenessReasons.ShouldBeEmpty();
    }

    [Fact]
    public void Summary_ShouldMarkIncompleteInvestmentsWithoutPrices()
    {
        var portfolio = InvestmentPortfolio(Euro);
        var buy = Buy(Etf(PriceUnit.PerUnit), 2.5m, portfolio, 80m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount()], [], [portfolio], [buy], [], [], ValuationDate);

        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IsComplete.ShouldBeFalse();
        summary.IncompletenessReasons.ShouldHaveSingleItem().ShouldContain("quotazione");
    }

    [Fact]
    public void Summary_ShouldRequireAChangeOfValuationForForeignPortfolioReferencedCurrencies()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Usd);
        var instrument = Instrument(Usd);
        var buy = Buy(instrument, 5m, portfolio, 22m, new DateOnly(2026, 3, 10));
        var price = Price(instrument, 22m, new DateOnly(2026, 3, 15), asPercent: false);

        var summary = NetWorthCalculator.Summarize(
            [checking], Incomes(checking, 100m, new DateOnly(2026, 3, 10)),
            [portfolio], [buy], [price], [], ValuationDate);

        summary.IsComplete.ShouldBeFalse();
        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldHaveSingleItem().ShouldContain("cambio");
    }

    [Fact]
    public void Summary_ShouldConvertForeignPortfolioValuationsUsingTheChangeOfValuation()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Usd);
        var instrument = Instrument(Usd);
        var buy = Buy(instrument, 5m, portfolio, 22m, new DateOnly(2026, 3, 10));
        var price = Price(instrument, 22m, new DateOnly(2026, 3, 15), asPercent: false);
        var usdRate = UsdObservation(1.10m, new DateOnly(2026, 3, 15));

        var summary = NetWorthCalculator.Summarize(
            [checking], Incomes(checking, 100m, new DateOnly(2026, 3, 10)),
            [portfolio], [buy], [price], [usdRate], ValuationDate);

        summary.InvestmentsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.TotalValue.ShouldBe(new Money(200m, Euro));
        summary.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Summary_ShouldRespectTheValuationDate()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Euro);
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10))
            .Concat(Incomes(checking, 500m, new DateOnly(2026, 4, 10)))
            .ToArray();
        var buy = Buy(Etf(PriceUnit.PerUnit), 2.5m, portfolio, 80m, new DateOnly(2026, 4, 10));
        var price = Price(Etf(PriceUnit.PerUnit), 90m, new DateOnly(2026, 4, 10), asPercent: false);

        var summary = NetWorthCalculator.Summarize(
            [checking], postings, [portfolio], [buy], [price], [], ValuationDate);

        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.InvestmentsSubtotal.ShouldBe(new Money(0m, Euro));
        summary.TotalValue.ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Summary_ShouldReuseTheBalanceCalculator()
    {
        var checking = CheckingAccount();
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10))
            .Concat(Expenses(checking, 30m, new DateOnly(2026, 3, 12)))
            .ToArray();

        var summary = NetWorthCalculator.Summarize(
            [checking], postings, [], [], [], [], ValuationDate);

        var expected = AccountBalanceCalculator.Balance(checking, postings, ValuationDate);
        summary.Accounts.ShouldHaveSingleItem().Balance.ShouldBe(expected);
    }

    [Fact]
    public void Summary_ShouldBeIndependentFromTheInputOrder()
    {
        var checking = CheckingAccount();
        var savings = Account("Risparmio", Euro);
        var portfolioOne = InvestmentPortfolio(Euro);
        var portfolioTwo = InvestmentPortfolio(Euro);
        var etfOne = Etf(PriceUnit.PerUnit);
        var etfTwo = Etf(PriceUnit.PerUnit);
        var postings = Incomes(checking, 100m, new DateOnly(2026, 3, 10))
            .Concat(Incomes(savings, 50m, new DateOnly(2026, 3, 11)))
            .ToArray();
        var buyOne = Buy(etfOne, 2m, portfolioOne, 80m, new DateOnly(2026, 3, 10));
        var buyTwo = Buy(etfTwo, 1m, portfolioTwo, 80m, new DateOnly(2026, 3, 11));
        var priceOne = Price(etfOne, 90m, new DateOnly(2026, 3, 20), asPercent: false);
        var priceTwo = Price(etfTwo, 50m, new DateOnly(2026, 3, 21), asPercent: false);

        var forward = NetWorthCalculator.Summarize(
            [checking, savings],
            postings,
            [portfolioOne, portfolioTwo],
            [buyOne, buyTwo],
            [priceOne, priceTwo],
            [],
            ValuationDate);
        var backward = NetWorthCalculator.Summarize(
            [savings, checking],
            postings.Reverse().ToArray(),
            [portfolioTwo, portfolioOne],
            [buyTwo, buyOne],
            [priceTwo, priceOne],
            [],
            ValuationDate);

        forward.TotalValue.ShouldBe(new Money(380m, Euro));
        backward.TotalValue.ShouldBe(forward.TotalValue);
        backward.AccountsSubtotal.ShouldBe(forward.AccountsSubtotal);
        backward.InvestmentsSubtotal.ShouldBe(forward.InvestmentsSubtotal);
    }

    [Fact]
    public void Summary_ShouldNotMutateTheProvidedCollections()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Euro);
        var postings = new List<AccountPosting>(Incomes(checking, 100m, new DateOnly(2026, 3, 10)));
        var priceObservations = new List<PriceObservation>();
        var exchangeRateObservations = new List<ExchangeRateObservation>();

        NetWorthCalculator.Summarize(
            [checking], postings, [portfolio], [], priceObservations, exchangeRateObservations, ValuationDate);

        postings.Count.ShouldBe(1);
        priceObservations.ShouldBeEmpty();
        exchangeRateObservations.ShouldBeEmpty();
    }

    [Fact]
    public void Summary_ShouldExposeReportingCurrencyFromTheSpecification()
    {
        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount()], Incomes(CheckingAccount(), 100m, new DateOnly(2026, 3, 10)),
            [], [], [], [], ValuationDate);

        summary.ReportingCurrency.ShouldBe(Euro);
    }

    [Fact]
    public void Summary_ShouldConvertGbpPortfolioValuesThroughEur()
    {
        var portfolio = InvestmentPortfolio(Gbp);
        var instrument = Etf(PriceUnit.PerUnit);
        var buy = Buy(instrument, 1m, portfolio, 80m, new DateOnly(2026, 3, 10));
        var price = Price(instrument, 100m, new DateOnly(2026, 3, 15), asPercent: false);
        var gbpRate = new ExchangeRateObservation(
            Guid.NewGuid(), new ExchangeRate(Gbp, 0.85m), new DateOnly(2026, 3, 15), RecordedAt, "ECB");

        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount()], [], [portfolio], [buy], [price], [gbpRate], ValuationDate);

        summary.InvestmentsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.TotalValue.ShouldBe(new Money(100m, Euro));
        summary.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Summary_ShouldRejectDuplicateAccountsById()
    {
        var checking = CheckingAccount();
        var duplicate = AccountWithId(checking.Id, "Conto duplicato", Euro);

        var exception = Should.Throw<ArgumentException>(
            () => NetWorthCalculator.Summarize(
                [checking, duplicate], [], [], [], [], [], ValuationDate));

        exception.ParamName.ShouldBe("accounts");
        exception.Message.ShouldContain("presente più volte");
    }

    [Fact]
    public void Summary_ShouldRejectDuplicatePortfoliosById()
    {
        var portfolio = InvestmentPortfolio(Euro);
        var duplicate = new InvestmentPortfolio(portfolio.Id, "Portafoglio duplicato", Euro);

        var exception = Should.Throw<ArgumentException>(
            () => NetWorthCalculator.Summarize(
                [CheckingAccount()], [], [portfolio, duplicate], [], [], [], ValuationDate));

        exception.ParamName.ShouldBe("portfolios");
        exception.Message.ShouldContain("presente più volte");
    }

    [Fact]
    public void Summary_ShouldRejectDuplicateInvestmentOperationsById()
    {
        var portfolio = InvestmentPortfolio(Euro);
        var buy = Buy(Etf(PriceUnit.PerUnit), 1m, portfolio, 80m, new DateOnly(2026, 3, 10));

        var exception = Should.Throw<ArgumentException>(
            () => NetWorthCalculator.Summarize(
                [CheckingAccount()], [], [portfolio], [buy, buy], [], [], ValuationDate));

        exception.ParamName.ShouldBe("operations");
        exception.Message.ShouldContain("presente più volte");
    }

    [Fact]
    public void Summary_ShouldMarkAnAccountIncompleteWhenItsExchangeRateIsStale()
    {
        var usdAccount = Account("Conto valute", Usd);
        var postings = Incomes(usdAccount, 55m, new DateOnly(2026, 3, 10));
        var staleRate = UsdObservation(1.10m, new DateOnly(2026, 3, 1));
        var policy = new ValuationPolicy(maximumExchangeRateAgeDays: 10);

        var summary = NetWorthCalculator.Summarize(
            [usdAccount], postings, [], [], [], [staleRate], ValuationDate, policy);

        var entry = summary.Accounts.ShouldHaveSingleItem();
        entry.IsComplete.ShouldBeFalse();
        entry.ExchangeRateObservation.ShouldBe(staleRate);
        entry.IncompletenessReason.ShouldNotBeNull();
        entry.IncompletenessReason!.ShouldContain("obsoleto");
        summary.AccountsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
    }

    [Fact]
    public void Summary_ShouldMarkInvestmentsIncompleteWhenThePriceIsStale()
    {
        var checking = CheckingAccount();
        var portfolio = InvestmentPortfolio(Euro);
        var etf = Etf(PriceUnit.PerUnit);
        var buy = Buy(etf, 2.5m, portfolio, 80m, new DateOnly(2026, 3, 10));
        var stalePrice = Price(etf, 90m, new DateOnly(2026, 3, 1), asPercent: false);
        var policy = new ValuationPolicy(maximumPriceAgeDays: 10);

        var summary = NetWorthCalculator.Summarize(
            [checking],
            Incomes(checking, 100m, new DateOnly(2026, 3, 10)),
            [portfolio],
            [buy],
            [stalePrice],
            [],
            ValuationDate,
            policy);

        summary.AccountsSubtotal.ShouldBe(new Money(100m, Euro));
        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldHaveSingleItem().ShouldContain("obsoleta");
    }

    [Fact]
    public void Summary_ShouldMarkInvestmentsIncompleteWhenThePortfolioReferenceRateIsStale()
    {
        var portfolio = InvestmentPortfolio(Usd);
        var instrument = Instrument(Usd);
        var buy = Buy(instrument, 5m, portfolio, 22m, new DateOnly(2026, 3, 10));
        var price = Price(instrument, 22m, new DateOnly(2026, 3, 15), asPercent: false);
        var staleRate = UsdObservation(1.10m, new DateOnly(2026, 3, 1));
        var policy = new ValuationPolicy(maximumExchangeRateAgeDays: 10);

        var summary = NetWorthCalculator.Summarize(
            [CheckingAccount()], [], [portfolio], [buy], [price], [staleRate], ValuationDate, policy);

        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
        summary.IncompletenessReasons.ShouldHaveSingleItem().ShouldContain("obsoleto");
    }

    [Fact]
    public void Summary_ShouldPreserveAllIncompletenessReasons()
    {
        var usdAccount = Account("Conto valute", Usd);
        var portfolio = InvestmentPortfolio(Euro);
        var buy = Buy(Etf(PriceUnit.PerUnit), 2.5m, portfolio, 80m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [usdAccount],
            Incomes(usdAccount, 55m, new DateOnly(2026, 3, 10)),
            [portfolio],
            [buy],
            [],
            [],
            ValuationDate);

        summary.IncompletenessReasons.Count.ShouldBe(2);
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("cambio"));
        summary.IncompletenessReasons.ShouldContain(reason => reason.Contains("quotazione"));
        summary.AccountsSubtotal.ShouldBeNull();
        summary.InvestmentsSubtotal.ShouldBeNull();
        summary.TotalValue.ShouldBeNull();
    }

    [Fact]
    public void Summary_ShouldAlwaysReportInEurEvenWhenInputsAreForeign()
    {
        var usdAccount = Account("Conto valute", Usd);
        var rate = UsdObservation(1.10m, new DateOnly(2026, 3, 10));

        var summary = NetWorthCalculator.Summarize(
            [usdAccount],
            Incomes(usdAccount, 55m, new DateOnly(2026, 3, 10)),
            [],
            [],
            [],
            [rate],
            ValuationDate);

        summary.ReportingCurrency.ShouldBe(Euro);
        summary.AccountsSubtotal!.Value.Currency.ShouldBe(Euro);
        summary.AccountsSubtotal.Value.Amount.ShouldBe(50m);
        summary.TotalValue!.Value.Currency.ShouldBe(Euro);
    }

    [Fact]
    public void Summary_ShouldCopyTheProvidedResultCollections()
    {
        var account = CheckingAccount();
        var accounts = new List<AccountBalanceEntry>
        {
            new(account, new Money(100m, Euro), new Money(100m, Euro), null, null),
        };
        var investments = new List<PortfolioValuation>();
        var reasons = new List<string> { "motivo" };

        var summary = new NetWorthSummary(
            ValuationDate,
            Euro,
            accounts,
            investments,
            new Money(100m, Euro),
            new Money(0m, Euro),
            new Money(100m, Euro),
            reasons);

        ReferenceEquals(summary.Accounts, accounts).ShouldBeFalse();
        ReferenceEquals(summary.Investments, investments).ShouldBeFalse();
        ReferenceEquals(summary.IncompletenessReasons, reasons).ShouldBeFalse();

        accounts.Clear();
        investments.Add(new PortfolioValuation(InvestmentPortfolio(Euro), ValuationDate, []));
        reasons.Clear();

        summary.Accounts.Count.ShouldBe(1);
        summary.Investments.ShouldBeEmpty();
        summary.IncompletenessReasons.Count.ShouldBe(1);
    }

    [Fact]
    public void Summary_ShouldRejectNullCollections()
    {
        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize(null!, [], [], [], [], [], ValuationDate))
            .ParamName.ShouldBe("accounts");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], null!, [], [], [], [], ValuationDate))
            .ParamName.ShouldBe("postings");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], null!, [], [], [], ValuationDate))
            .ParamName.ShouldBe("portfolios");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], [], null!, [], [], ValuationDate))
            .ParamName.ShouldBe("operations");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], [], [], null!, [], ValuationDate))
            .ParamName.ShouldBe("priceObservations");

        Should.Throw<ArgumentNullException>(
                () => NetWorthCalculator.Summarize([], [], [], [], [], null!, ValuationDate))
            .ParamName.ShouldBe("exchangeRateObservations");
    }

    // ------------- helper -------------

    private static Account CheckingAccount() => Account("Conto corrente", Euro);

    private static Account AccountWithId(Guid id, string name, Currency currency) => new(
        id, name, AccountType.Checking, currency, AccountStatus.Active);

    private static Account Account(string name, Currency currency, AccountType type = AccountType.Checking) => new(
        Guid.NewGuid(), name, type, currency, AccountStatus.Active);

    private static InvestmentPortfolio InvestmentPortfolio(Currency referenceCurrency) => new(
        Guid.NewGuid(),
        "Portafoglio " + referenceCurrency.Code,
        referenceCurrency);

    private static Instrument Instrument(Currency quote) => new(
        Guid.NewGuid(), "Strumento " + quote.Code, InstrumentType.Etf, quote, PriceUnit.PerUnit);

    private static Instrument Etf(PriceUnit priceUnit) => new(
        Guid.NewGuid(), "ETF globale", InstrumentType.Etf, Euro, priceUnit);

    private static IReadOnlyList<AccountPosting> Incomes(Account account, decimal amount, DateOnly date) =>
        OrdinaryPostings(OperationKind.Income, account, amount, date);

    private static IReadOnlyList<AccountPosting> Expenses(Account account, decimal amount, DateOnly date) =>
        OrdinaryPostings(OperationKind.Expense, account, amount, date);

    private static IReadOnlyList<AccountPosting> OrdinaryPostings(
        OperationKind kind, Account account, decimal amount, DateOnly date)
    {
        var amountToRecord = new PositiveMoney(new Money(amount, account.Currency));
        var operation = kind == OperationKind.Income
            ? (FinancialOperation)new Income(Guid.NewGuid(), account, Groceries, amountToRecord, date, RecordedAt)
            : new Expense(Guid.NewGuid(), account, Groceries, amountToRecord, date, RecordedAt);
        return operation.GeneratePostings();
    }

    private static InvestmentOperation Buy(
        Instrument instrument, decimal quantity, InvestmentPortfolio portfolio, decimal unitPrice, DateOnly date) =>
        InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            SettlementAccountFor(instrument),
            new Quantity(quantity),
            instrument.PriceUnit == PriceUnit.PercentOfNominal
                ? QuotedPrice.AsPercentOfNominal(unitPrice)
                : Quote(unitPrice, instrument.QuoteCurrency),
            null,
            date,
            RecordedAt);

    private static QuotedPrice Quote(decimal price, Currency currency) =>
        QuotedPrice.AtUnitPrice(new UnitPrice(new Money(price, currency)));

    private static Account SettlementAccountFor(Instrument instrument) =>
        Account("Conto valute", instrument.QuoteCurrency);

    private static PriceObservation Price(
        Instrument instrument, decimal price, DateOnly validAt, bool asPercent) =>
        asPercent
            ? Domain.Portfolio.PriceObservation.AsPercentOfNominal(
                Guid.NewGuid(), instrument, price, validAt, RecordedAt, "Manuale")
            : Domain.Portfolio.PriceObservation.AtUnitPrice(
                Guid.NewGuid(), instrument, new UnitPrice(new Money(price, instrument.QuoteCurrency)), validAt, RecordedAt, "Manuale");

    private static ExchangeRateObservation UsdObservation(decimal rate, DateOnly validAt) => new(
        Guid.NewGuid(), new ExchangeRate(Usd, rate), validAt, RecordedAt, "ECB");
}
