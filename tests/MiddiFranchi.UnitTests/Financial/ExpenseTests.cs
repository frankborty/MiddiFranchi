using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class ExpenseTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly Guid OperationId = Guid.Parse("67839f70-137b-4e73-ad3c-743e6cba0eb6");
    private static readonly Guid AccountId = Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4");
    private static readonly Guid CategoryId = Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668");
    private static readonly DateOnly OperationDate = new(2026, 3, 10);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 11, 9, 30, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        AccountId, "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Category Electronics = new(
        CategoryId, "Elettronica", CategoryType.Expense);

    [Fact]
    public void GeneratePostings_ShouldProduceOneNegativePostingToTheAccount()
    {
        var expense = new Expense(
            OperationId,
            CheckingAccount,
            Electronics,
            new PositiveMoney(new Money(42.50m, Euro)),
            OperationDate,
            RecordedAt);

        var postings = expense.GeneratePostings();

        postings.Count.ShouldBe(1);
        var posting = postings.Single();
        posting.OperationId.ShouldBe(OperationId);
        posting.AccountId.ShouldBe(AccountId);
        posting.Kind.ShouldBe(PostingKind.Capital);
        posting.Currency.ShouldBe(Euro);
        posting.SignedAmount.ShouldBe(-42.50m);
        posting.OperationDate.ShouldBe(OperationDate);
    }

    [Fact]
    public void GeneratePostings_ShouldApplyTheSignOnlyOnce()
    {
        var expense = new Expense(
            OperationId,
            CheckingAccount,
            Electronics,
            new PositiveMoney(new Money(100m, Euro)),
            OperationDate,
            RecordedAt);

        var postedSum = expense.GeneratePostings().Sum(posting => posting.SignedAmount);

        postedSum.ShouldBe(-expense.Amount.Value.Amount);
        postedSum.ShouldBe(-100m);
    }


    [Fact]
    public void Expense_ShouldExposeTheExpenseKind()
    {
        var expense = new Expense(
            OperationId,
            CheckingAccount,
            Electronics,
            new PositiveMoney(new Money(42.50m, Euro)),
            OperationDate,
            RecordedAt);

        expense.Kind.ShouldBe(OperationKind.Expense);
    }
}
