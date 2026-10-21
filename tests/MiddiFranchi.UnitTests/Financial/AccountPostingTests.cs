using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class AccountPostingTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Guid OperationId = Guid.Parse("67839f70-137b-4e73-ad3c-743e6cba0eb6");
    private static readonly Guid AccountId = Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4");
    private static readonly DateOnly OperationDate = new(2026, 3, 10);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 11, 9, 30, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        AccountId, "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Category GenericCategory = new(
        Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668"), "Generica", CategoryType.Income);

    [Fact]
    public void Construction_ShouldExposeThePostingDataAndItsSourceOperation()
    {
        var operation = new Income(
            OperationId,
            CheckingAccount,
            GenericCategory,
            new PositiveMoney(new Money(42.50m, Euro)),
            OperationDate,
            RecordedAt);

        var posting = new AccountPosting(operation, AccountId, PostingKind.Capital, Euro, -42.50m);

        posting.Operation.ShouldBe(operation);
        posting.OperationId.ShouldBe(OperationId);
        posting.SignedAmount.ShouldBe(-42.50m);
        posting.Kind.ShouldBe(PostingKind.Capital);
        posting.OperationDate.ShouldBe(OperationDate);
    }

    [Fact]
    public void Construction_ShouldRejectAMissingOperationOrAccount()
    {
        var operation = IncomeOperation();

        Should.Throw<ArgumentNullException>(
                () => new AccountPosting(null!, AccountId, PostingKind.Income, Euro, 1m))
            .ParamName.ShouldBe("operation");

        Should.Throw<ArgumentException>(
                () => new AccountPosting(operation, Guid.Empty, PostingKind.Income, Euro, 1m))
            .ParamName.ShouldBe("accountId");
    }

    [Fact]
    public void Construction_ShouldRejectNonEconomicSigns()
    {
        var operation = IncomeOperation();

        Should.Throw<ArgumentException>(
                () => new AccountPosting(operation, AccountId, PostingKind.Capital, default, -1m))
            .ParamName.ShouldBe("currency");

        Should.Throw<ArgumentException>(
                () => new AccountPosting(operation, AccountId, PostingKind.Capital, Euro, 0m))
            .ParamName.ShouldBe("signedAmount");

        Should.Throw<ArgumentException>(
                () => new AccountPosting(operation, AccountId, PostingKind.Income, Euro, -1m))
            .ParamName.ShouldBe("signedAmount");

        Should.Throw<ArgumentException>(
                () => new AccountPosting(operation, AccountId, PostingKind.Fee, Euro, 1m))
            .ParamName.ShouldBe("signedAmount");
    }

    private static Income IncomeOperation() => new(
        OperationId,
        CheckingAccount,
        GenericCategory,
        new PositiveMoney(new Money(42.50m, Euro)),
        OperationDate,
        RecordedAt);
}
