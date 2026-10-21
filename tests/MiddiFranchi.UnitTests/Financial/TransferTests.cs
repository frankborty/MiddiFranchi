using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class TransferTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Guid OperationId = Guid.Parse("8a92bb63-c3d4-4a01-9f9f-4c8a0d3f1e2d");
    private static readonly DateOnly OperationDate = new(2026, 3, 12);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 12, 15, 0, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account SavingsAccount = new(
        Guid.Parse("6c7f1e09-8f2d-4c6b-bd0e-c8a4c8402f9c"), "Risparmio", AccountType.Checking, Euro, AccountStatus.Active);

    [Fact]
    public void Transfer_ShouldExposeTheTransferKind()
    {
        Transfer(300m).Kind.ShouldBe(OperationKind.Transfer);
    }

    [Fact]
    public void Generation_ShouldProduceTwoLinkedPostingsInTheTransferLegKind()
    {
        var postings = Transfer(300m).GeneratePostings();

        postings.Count.ShouldBe(2);
        postings.ShouldAllBe(posting => posting.Kind == PostingKind.TransferLeg);
        postings.ShouldAllBe(posting => posting.OperationId == OperationId);
        postings.ShouldAllBe(posting => posting.OperationDate == OperationDate);
    }

    [Fact]
    public void Generation_ShouldApplyTheNegativeSignToTheSourceAndThePositiveSignToTheDestination()
    {
        var postings = Transfer(300m).GeneratePostings();
        var source = postings.Single(posting => posting.AccountId == CheckingAccount.Id);
        var destination = postings.Single(posting => posting.AccountId == SavingsAccount.Id);

        source.SignedAmount.ShouldBe(-300m);
        source.Currency.ShouldBe(Euro);
        destination.SignedAmount.ShouldBe(300m);
        destination.Currency.ShouldBe(Euro);
    }

    [Fact]
    public void Construction_ShouldRejectTheSameAccount()
    {
        Should.Throw<ArgumentException>(
                () => Transfer(CheckingAccount, CheckingAccount, 300m))
            .ParamName.ShouldBe("destinationAccount");
    }

    [Fact]
    public void Construction_ShouldRejectAccountsThatShareTheSameIdentifier()
    {
        var sameIdentifierAccount = new Account(
            CheckingAccount.Id, "Stesso identificatore", AccountType.Checking, Euro, AccountStatus.Active);

        Should.Throw<ArgumentException>(
                () => Transfer(CheckingAccount, sameIdentifierAccount, 300m))
            .ParamName.ShouldBe("destinationAccount");
    }

    [Fact]
    public void Construction_ShouldRejectNonPositiveAmounts()
    {
        Should.Throw<ArgumentException>(() => Transfer(0m));
        Should.Throw<ArgumentException>(() => Transfer(-1m));
    }

    [Fact]
    public void Construction_ShouldRequireEqualAmounts()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                CheckingAccount,
                SavingsAccount,
                Eur(300m),
                Eur(200m),
                OperationDate,
                RecordedAt));

        exception.Message.ShouldContain("devono coincidere");
    }

    [Fact]
    public void Balance_ShouldIntegrateThePostingsOfBothAccounts()
    {
        var postings = Transfer(300m).GeneratePostings();

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, OperationDate)
            .ShouldBe(new Money(-300m, Euro));
        AccountBalanceCalculator
            .Balance(SavingsAccount, postings, OperationDate)
            .ShouldBe(new Money(300m, Euro));
    }

    private static Transfer Transfer(decimal amount) =>
        Transfer(CheckingAccount, SavingsAccount, amount);

    private static Transfer Transfer(Account source, Account destination, decimal amount) =>
        new(OperationId, source, destination, Eur(amount), Eur(amount), OperationDate, RecordedAt);

    private static PositiveMoney Eur(decimal amount) => new(new Money(amount, Euro));
}
