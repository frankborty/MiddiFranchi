using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class FeeComponentTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly Account EuroAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"),
        "Conto corrente",
        AccountType.Checking,
        Euro,
        AccountStatus.Active);

    [Fact]
    public void Construction_ShouldAcceptACommissionOnAnAccountWithTheSameCurrency()
    {
        var fee = new FeeComponent(
            FeeKind.BuyFee,
            new PositiveMoney(new Money(1.50m, Euro)),
            EuroAccount);

        fee.Kind.ShouldBe(FeeKind.BuyFee);
        fee.Amount.ShouldBe(new PositiveMoney(new Money(1.50m, Euro)));
        fee.DebitAccount.ShouldBe(EuroAccount);
    }


    [Fact]
    public void Construction_ShouldAllowANullDebitAccount()
    {
        var fee = new FeeComponent(
            FeeKind.SellFee,
            new PositiveMoney(new Money(1.50m, Euro)),
            debitAccount: null);

        fee.DebitAccount.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Construction_ShouldRejectNonPositiveFeeAmounts(double rawAmount)
    {
        var amount = Convert.ToDecimal(rawAmount, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentException>(
            () => new FeeComponent(FeeKind.BuyFee, new PositiveMoney(new Money(amount, Euro)), EuroAccount))
            .Message.ShouldContain("positivo");
    }
}
