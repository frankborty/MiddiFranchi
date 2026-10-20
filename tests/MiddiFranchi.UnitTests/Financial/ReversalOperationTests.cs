using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class ReversalOperationTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Guid OperationId = Guid.Parse("8a92bb63-c3d4-4a01-9f9f-4c8a0d3f1e2d");
    private static readonly Guid ReversalId = Guid.Parse("67839f70-137b-4e73-ad3c-743e6cba0eb6");
    private static readonly Guid AccountId = Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4");
    private static readonly Guid DestinationAccountId = Guid.Parse("6c7f1e09-8f2d-4c6b-bd0e-c8a4c8402f9c");
    private static readonly Guid CategoryId = Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668");
    private static readonly DateOnly OriginalDate = new(2026, 3, 12);
    private static readonly DateTimeOffset OriginalRecordedAt = new(2026, 3, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ReversalDate = new(2026, 4, 20);
    private static readonly DateTimeOffset ReversalRecordedAt = new(2026, 4, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        AccountId, "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account SavingsAccount = new(
        DestinationAccountId, "Risparmio", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Category GenericCategory = new(
        CategoryId, "Generica", CategoryType.Income);

    [Fact]
    public void Reversal_ShouldLinkTheOriginalOperation()
    {
        var income = Income(100m);
        var reversal = new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);

        reversal.Original.ShouldBe(income);
        reversal.OriginalOperationId.ShouldBe(income.Id);
        reversal.Kind.ShouldBe(OperationKind.Reversal);
        reversal.Status.ShouldBe(OperationStatus.Registered);
        reversal.ReversalOperationId.ShouldBeNull();
        income.Status.ShouldBe(OperationStatus.Reversed);
        income.ReversalOperationId.ShouldBe(ReversalId);
    }

    [Fact]
    public void Reversal_ShouldNotGenerateItsOwnPostings()
    {
        var reversal = new ReversalOperation(ReversalId, Income(100m), ReversalDate, ReversalRecordedAt);

        reversal.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public void Reversal_ShouldNeutralizeAnIncome()
    {
        var income = Income(100m);

        income.GeneratePostings().Count.ShouldBe(1);
        new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);
        income.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public void Reversal_ShouldNeutralizeAnExpense()
    {
        var expense = Expense(50m);

        expense.GeneratePostings().Count.ShouldBe(1);
        new ReversalOperation(ReversalId, expense, ReversalDate, ReversalRecordedAt);
        expense.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public void Reversal_ShouldNeutralizeBothSidesOfATransfer()
    {
        var transfer = Transfer(300m, CheckingAccount, SavingsAccount);

        transfer.GeneratePostings().Count.ShouldBe(2);
        var reversal = new ReversalOperation(ReversalId, transfer, ReversalDate, ReversalRecordedAt);
        transfer.GeneratePostings().ShouldBeEmpty();
        reversal.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public void Reversal_ShouldRejectAnAlreadyReversedOperation()
    {
        var income = Income(100m);
        new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);

        var exception = Should.Throw<ArgumentException>(
            () => new ReversalOperation(Guid.NewGuid(), income, ReversalDate, ReversalRecordedAt));

        exception.Message.ShouldContain("già stornata");
    }

    [Fact]
    public void Reversal_ShouldRejectAReversalOfAReversal()
    {
        var income = Income(100m);
        var reversal = new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);

        var exception = Should.Throw<ArgumentException>(
            () => new ReversalOperation(Guid.NewGuid(), reversal, ReversalDate, ReversalRecordedAt));

        exception.Message.ShouldContain("non può essere stornato");
    }

    [Fact]
    public void Reversal_ShouldRejectANullOriginal()
    {
        Should.Throw<ArgumentNullException>(
            () => new ReversalOperation(ReversalId, null!, ReversalDate, ReversalRecordedAt));
    }

    [Fact]
    public void Balance_ShouldExcludeOnlyTheReversedOperation()
    {
        var unrelated = Income(50m);
        var income = Income(100m);
        var reversal = new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);

        var postings = PostingsOf(unrelated, income);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, ReversalDate)
            .ShouldBe(new Money(50m, Euro));
    }

    [Fact]
    public void Balance_ShouldIgnorePostingsMaterializedBeforeTheReversal()
    {
        var unrelated = Income(50m, new DateOnly(2026, 3, 1));
        var income = Income(100m, new DateOnly(2026, 3, 12));
        var postings = PostingsOf(unrelated, income);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, ReversalDate)
            .ShouldBe(new Money(150m, Euro));

        var reversal = new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);
        reversal.Original.ShouldBe(income);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, ReversalDate)
            .ShouldBe(new Money(50m, Euro));
        AccountBalanceCalculator
            .Balance(CheckingAccount, PostingsOf(unrelated), ReversalDate)
            .ShouldBe(new Money(50m, Euro));
    }

    [Fact]
    public void Balance_ShouldApplyTheGlobalReversalSemanticsToEarlierEconomicDates()
    {
        var income = Income(100m, new DateOnly(2026, 3, 12));
        var postings = PostingsOf(income);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, new DateOnly(2026, 3, 12))
            .ShouldBe(new Money(100m, Euro));

        var reversal = new ReversalOperation(ReversalId, income, ReversalDate, ReversalRecordedAt);
        reversal.Id.ShouldNotBe(Guid.Empty);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, new DateOnly(2026, 3, 12))
            .ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public void Balance_ShouldRemainIndependentFromTheReversalRecordedAt()
    {
        var unrelated = Income(50m, new DateOnly(2026, 3, 1));
        var income = Income(100m, new DateOnly(2026, 3, 12));
        var postings = PostingsOf(unrelated, income);
        var reversal = new ReversalOperation(
            ReversalId, income, ReversalDate, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        reversal.Id.ShouldNotBe(Guid.Empty);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, new DateOnly(2026, 3, 15))
            .ShouldBe(new Money(50m, Euro));
        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, new DateOnly(2026, 12, 31))
            .ShouldBe(new Money(50m, Euro));
    }

    [Fact]
    public void Balance_ShouldShowZeroOnBothAccountsAfterATransferReversal()
    {
        var transfer = Transfer(300m, CheckingAccount, SavingsAccount);
        var postings = PostingsOf(transfer);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, ReversalDate)
            .ShouldBe(new Money(-300m, Euro));
        AccountBalanceCalculator
            .Balance(SavingsAccount, postings, ReversalDate)
            .ShouldBe(new Money(300m, Euro));

        var reversal = new ReversalOperation(ReversalId, transfer, ReversalDate, ReversalRecordedAt);
        reversal.Id.ShouldNotBe(Guid.Empty);

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, ReversalDate)
            .ShouldBe(new Money(0m, Euro));
        AccountBalanceCalculator
            .Balance(SavingsAccount, postings, ReversalDate)
            .ShouldBe(new Money(0m, Euro));
    }

    [Fact]
    public void Balance_ShouldLeaveUnrelatedPostingsUnchanged()
    {
        var unrelated = Income(50m);
        new ReversalOperation(ReversalId, Income(100m), ReversalDate, ReversalRecordedAt);

        unrelated.GeneratePostings().Count.ShouldBe(1);
        unrelated.GeneratePostings().Single().SignedAmount.ShouldBe(50m);
        unrelated.GeneratePostings().Single().OperationId.ShouldBe(unrelated.Id);
    }

    private static IReadOnlyList<AccountPosting> PostingsOf(params FinancialOperation[] operations) =>
        operations.SelectMany(operation => operation.GeneratePostings()).ToArray();

    private static Income Income(decimal amount, DateOnly? date = null) => new(
        Guid.NewGuid(),
        CheckingAccount,
        GenericCategory,
        new PositiveMoney(new Money(amount, Euro)),
        date ?? OriginalDate,
        OriginalRecordedAt);

    private static Expense Expense(decimal amount) => new(
        Guid.NewGuid(),
        CheckingAccount,
        GenericCategory,
        new PositiveMoney(new Money(amount, Euro)),
        OriginalDate,
        OriginalRecordedAt);

    private static Transfer Transfer(decimal amount, Account source, Account destination) => new(
        Guid.NewGuid(),
        source,
        destination,
        new PositiveMoney(new Money(amount, source.Currency)),
        new PositiveMoney(new Money(amount, destination.Currency)),
        null,
        OriginalDate,
        OriginalRecordedAt);
}
