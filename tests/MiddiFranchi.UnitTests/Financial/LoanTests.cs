using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class LoanTests
{
    private static readonly Currency Euro = Currency.Euro;

    private static Account LoansAccount() => new(
        Guid.NewGuid(), "Prestiti", AccountType.Loans, Euro, AccountStatus.Active);

    [Fact]
    public void Create_ShouldNormalizeTheBorrowerAndKeepTheAccount()
    {
        var account = LoansAccount();

        var loan = new Loan(Guid.NewGuid(), account, "  Mario  ", DateTimeOffset.UtcNow);

        loan.Borrower.ShouldBe("Mario");
        loan.Account.ShouldBe(account);
        loan.AccountId.ShouldBe(account.Id);
    }

    [Fact]
    public void Create_ShouldRejectAnEmptyIdentifierOrBorrower()
    {
        Should.Throw<ArgumentException>(() =>
            new Loan(Guid.Empty, LoansAccount(), "Mario", DateTimeOffset.UtcNow));
        Should.Throw<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), LoansAccount(), " ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_ShouldRejectANonLoansAccount()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);

        Should.Throw<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), account, "Mario", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void LoanReading_ShouldUseTheAccountCurrency()
    {
        var loan = new Loan(Guid.NewGuid(), LoansAccount(), "Mario", DateTimeOffset.UtcNow);

        var reading = new LoanReading(
            Guid.NewGuid(),
            loan,
            new Money(10m, Euro),
            new DateOnly(2026, 4, 1),
            DateTimeOffset.UtcNow);

        reading.Amount.ShouldBe(new Money(10m, Euro));
        reading.Loan.ShouldBe(loan);
        reading.LoanId.ShouldBe(loan.Id);
    }
}
