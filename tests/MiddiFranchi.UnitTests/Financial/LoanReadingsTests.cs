using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class LoanReadingsTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 21);

    [Fact]
    public void TotalOnOrBefore_ShouldSumTheLatestReadingOfEachLoanAndTreatMissingAsZero()
    {
        var account = new Account(
            Guid.NewGuid(), "Prestiti", AccountType.Loans, Euro, AccountStatus.Active);
        var mario = new Loan(Guid.NewGuid(), account, "Mario", DateTimeOffset.UtcNow);
        var lucia = new Loan(Guid.NewGuid(), account, "Lucia", DateTimeOffset.UtcNow);
        var readings = new List<LoanReading>
        {
            Reading(mario, 100m, new DateOnly(2026, 9, 1)),
            Reading(mario, 80m, new DateOnly(2026, 10, 1)),
            Reading(lucia, 50m, new DateOnly(2026, 8, 1)),
        };

        var total = LoanReadings.TotalOnOrBefore([mario, lucia], readings, Today, Euro);

        total.ShouldBe(new Money(130m, Euro));
    }

    [Fact]
    public void LatestOnOrBefore_ShouldPickTheMostRecentValidReading()
    {
        var account = new Account(
            Guid.NewGuid(), "Prestiti", AccountType.Loans, Euro, AccountStatus.Active);
        var loan = new Loan(Guid.NewGuid(), account, "Mario", DateTimeOffset.UtcNow);
        var older = Reading(loan, 100m, new DateOnly(2026, 9, 1));
        var newer = Reading(loan, 80m, new DateOnly(2026, 10, 1));

        var result = LoanReadings.LatestOnOrBefore([older, newer], loan.Id, new DateOnly(2026, 9, 15));

        result.ShouldBe(older);
        LoanReadings.LatestOnOrBefore([older, newer], loan.Id, new DateOnly(2026, 8, 31)).ShouldBeNull();
    }

    private static LoanReading Reading(Loan loan, decimal amount, DateOnly validAt) =>
        new(
            Guid.NewGuid(),
            loan,
            new Money(amount, Euro),
            validAt);
}
