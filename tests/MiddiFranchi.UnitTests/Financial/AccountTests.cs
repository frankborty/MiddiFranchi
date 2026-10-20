using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class AccountTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");

    [Fact]
    public void Construction_ShouldAcceptValidAccounts()
    {
        var account = new Account(
            Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"),
            "Conto corrente",
            AccountType.Checking,
            Euro,
            AccountStatus.Active);

        account.Name.ShouldBe("Conto corrente");
        account.Type.ShouldBe(AccountType.Checking);
        account.Currency.ShouldBe(Euro);
        account.Status.ShouldBe(AccountStatus.Active);
    }

    [Fact]
    public void Construction_ShouldRejectAnEmptyIdentifier()
    {
        Should.Throw<ArgumentException>(
            () => new Account(Guid.Empty, "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active))
            .ParamName.ShouldBe("id");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultCurrency()
    {
        Should.Throw<ArgumentException>(
            () => new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, default, AccountStatus.Active))
            .ParamName.ShouldBe("currency");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_ShouldRejectABlankName(string? name)
    {
        Should.Throw<ArgumentException>(
            () => new Account(Guid.NewGuid(), name!, AccountType.Checking, Euro, AccountStatus.Active))
            .ParamName.ShouldBe("name");
    }
}
