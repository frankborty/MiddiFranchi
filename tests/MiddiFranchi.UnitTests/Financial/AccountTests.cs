using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class AccountTests
{
    private static readonly Currency Euro = Currency.Euro;

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

    [Fact]
    public void Construction_ShouldAcceptAndNormalizeCustomColorAndIcon()
    {
        var account = new Account(
            Guid.NewGuid(),
            "Conto corrente",
            AccountType.Checking,
            Euro,
            AccountStatus.Active,
            "#FF8800",
            "data:image/png;base64,AAAA");

        account.ColorHex.ShouldBe("#ff8800");
        account.IconDataUrl.ShouldBe("data:image/png;base64,AAAA");
    }

    [Fact]
    public void Construction_ShouldKeepColorAndIconNullWhenMissing()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);

        account.ColorHex.ShouldBeNull();
        account.IconDataUrl.ShouldBeNull();
    }

    [Theory]
    [InlineData("ff8800")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    public void Construction_ShouldRejectAnInvalidColor(string colorHex)
    {
        Should.Throw<ArgumentException>(() => new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active, colorHex))
            .ParamName.ShouldBe("colorHex");
    }

    [Fact]
    public void Construction_ShouldRejectANonImageIcon()
    {
        Should.Throw<ArgumentException>(() => new Account(
            Guid.NewGuid(),
            "Conto corrente",
            AccountType.Checking,
            Euro,
            AccountStatus.Active,
            iconDataUrl: "testo"))
            .ParamName.ShouldBe("iconDataUrl");
    }
}
