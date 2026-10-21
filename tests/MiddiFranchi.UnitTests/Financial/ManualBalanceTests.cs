using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class ManualBalanceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Currency Usd = Currency.Euro;
    private static readonly Account Account = new(
        Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly DateOnly ValidAt = new(2026, 4, 1);
    private static readonly DateTimeOffset RecordedAt = new(2026, 4, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reading_ShouldExposeItsComponents()
    {
        var reading = new ManualBalance(
            Guid.NewGuid(), Account, new Money(1234.56789m, Euro), ValidAt, RecordedAt);

        reading.Account.ShouldBe(Account);
        reading.AccountId.ShouldBe(Account.Id);
        reading.Amount.ShouldBe(new Money(1234.56789m, Euro));
        reading.ValidAt.ShouldBe(ValidAt);
        reading.RecordedAt.ShouldBe(RecordedAt);
    }

    [Fact]
    public void Reading_ShouldAcceptNegativeAndZeroObservedBalances()
    {
        var negative = new ManualBalance(Guid.NewGuid(), Account, new Money(-40m, Euro), ValidAt, RecordedAt);
        var zero = new ManualBalance(Guid.NewGuid(), Account, new Money(0m, Euro), ValidAt, RecordedAt);

        negative.Amount.Amount.ShouldBe(-40m);
        zero.Amount.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Reading_ShouldRejectAnIdentityThatIsEmpty()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ManualBalance(Guid.Empty, Account, new Money(10m, Euro), ValidAt, RecordedAt));

        exception.Message.ShouldContain("L'identificativo della rilevazione non può essere vuoto");
    }

    [Fact]
    public void Reading_ShouldRejectAMissingAccount()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ManualBalance(Guid.NewGuid(), null!, new Money(10m, Euro), ValidAt, RecordedAt));

        exception.ParamName.ShouldBe("account");
    }

    [Fact]
    public void Reading_ShouldRejectAMissingCurrency()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ManualBalance(Guid.NewGuid(), Account, default(Money), ValidAt, RecordedAt));

        exception.Message.ShouldContain("La valuta della rilevazione è obbligatoria");
    }

}
