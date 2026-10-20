using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class AccountBalanceCalculatorTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Guid AccountId = Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4");
    private static readonly Guid OtherAccountId = Guid.Parse("2ff8522f-e1b9-4feb-a0a0-a3cff9603cbf");
    private static readonly Guid CategoryId = Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668");
    private static readonly DateOnly AsOf = new(2026, 3, 10);
    private static readonly DateTimeOffset RecordedLater = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        AccountId, "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account CardAccount = new(
        Guid.Parse("c1c2b06c-b415-4a95-a4b5-5a56e5aefd9c"), "Carta di credito", AccountType.Card, Euro, AccountStatus.Active);
    private static readonly Account OtherAccount = new(
        OtherAccountId, "Altro conto", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Category Generic = new(CategoryId, "Generica", CategoryType.Income);

    [Fact]
    public void Balance_ShouldReturnZeroForAnAccountWithoutMovements()
    {
        AccountBalanceCalculator
            .Balance(CheckingAccount, [], AsOf)
            .ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public void Balance_ShouldSumOnlyIncomes()
    {
        var postings = PostingsFrom(
            IncomeOn(2026, 3, 1, 42.50m),
            IncomeOn(2026, 3, 5, 57.50m));

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, AsOf)
            .ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Balance_ShouldAllowNegativeBalanceFromExpensesOnly()
    {
        var cardPostings = PostingsFrom(
            ExpenseOn(CardAccount, 2026, 3, 4, 30m),
            ExpenseOn(CardAccount, 2026, 3, 9, 20m));

        AccountBalanceCalculator
            .Balance(CardAccount, cardPostings, AsOf)
            .ShouldBe(new Money(-50m, Euro));
    }

    [Fact]
    public void Balance_ShouldNetIncomesAndExpensesTogether()
    {
        var postings = PostingsFrom(
            IncomeOn(2026, 3, 1, 100m),
            IncomeOn(2026, 3, 5, 50m),
            ExpenseOn(CheckingAccount, 2026, 3, 8, 70m));

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, AsOf)
            .ShouldBe(new Money(80m, Euro));
    }

    [Fact]
    public void Balance_ShouldExcludePostingsAfterTheRequestedDate()
    {
        var postings = PostingsFrom(
            IncomeOn(2026, 3, 5, 100m),
            ExpenseOn(CheckingAccount, 2026, 3, 15, 70m));

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, AsOf)
            .ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Balance_ShouldIncludePostingsExactlyOnTheRequestedDate()
    {
        var postings = PostingsFrom(IncomeOn(2026, 3, 10, 100m));

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, AsOf)
            .ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Balance_ShouldReconstructFromTheEconomicDateWhenRecordedAtIsLater()
    {
        var income = new Income(
            Guid.NewGuid(),
            CheckingAccount,
            Generic,
            new PositiveMoney(new Money(42.50m, Euro)),
            new DateOnly(2026, 3, 1),
            RecordedLater);

        AccountBalanceCalculator
            .Balance(CheckingAccount, income.GeneratePostings(), new DateOnly(2026, 3, 5))
            .ShouldBe(new Money(42.50m, Euro));
    }

    [Fact]
    public void Balance_ShouldIgnorePostingsOfOtherAccounts()
    {
        var postings = PostingsFrom(
            IncomeOn(2026, 3, 1, 100m),
            IncomeOn(OtherAccount, 2026, 3, 2, 999m));

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, AsOf)
            .ShouldBe(new Money(100m, Euro));
    }

    [Fact]
    public void Balance_ShouldRejectNullArguments()
    {
        Should.Throw<ArgumentNullException>(
                () => AccountBalanceCalculator.Balance(null!, [], AsOf))
            .ParamName.ShouldBe("account");

        Should.Throw<ArgumentNullException>(
                () => AccountBalanceCalculator.Balance(CheckingAccount, null!, AsOf))
            .ParamName.ShouldBe("postings");
    }

    [Fact]
    public void Balance_ShouldRejectPostingsInADifferentCurrencyThanTheAccount()
    {
        var operation = IncomeOn(2026, 3, 5, 10m);
        var foreignPosting = new AccountPosting(
            operation,
            AccountId,
            PostingKind.Income,
            Usd,
            10m);

        var exception = Should.Throw<InvalidOperationException>(
            () => AccountBalanceCalculator.Balance(CheckingAccount, [foreignPosting], AsOf));

        exception.Message.ShouldContain("diversa dalla valuta del conto");
    }

    private static IReadOnlyList<AccountPosting> PostingsFrom(params FinancialOperation[] operations) =>
        operations.SelectMany(operation => operation.GeneratePostings()).ToArray();

    private static Income IncomeOn(int year, int month, int day, decimal amount) =>
        IncomeOn(CheckingAccount, year, month, day, amount);

    private static Income IncomeOn(Account account, int year, int month, int day, decimal amount) =>
        new(
            Guid.NewGuid(),
            account,
            Generic,
            new PositiveMoney(new Money(amount, Euro)),
            new DateOnly(year, month, day),
            new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));

    private static Expense ExpenseOn(Account account, int year, int month, int day, decimal amount) =>
        new(
            Guid.NewGuid(),
            account,
            Generic,
            new PositiveMoney(new Money(amount, Euro)),
            new DateOnly(year, month, day),
            new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero));
}
