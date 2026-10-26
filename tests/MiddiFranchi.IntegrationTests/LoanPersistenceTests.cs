using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class LoanPersistenceTests : PostgresTestBase
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 21);

    public LoanPersistenceTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task LoanAndReadings_ShouldRoundTrip()
    {
        var account = await AddLoansAccountAsync();
        var loans = new LoanRepository(Context);
        var readings = new LoanReadingRepository(Context);
        var loan = new Loan(
            Guid.NewGuid(),
            account,
            "Mario",
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

        await loans.AddAsync(loan);
        var reading = new LoanReading(
            Guid.NewGuid(),
            loan,
            new Money(100m, Euro),
            new DateOnly(2026, 10, 1));
        await readings.AddAsync(reading);
        ClearTracker();

        var loadedLoan = (await loans.GetByIdAsync(loan.Id)).ShouldNotBeNull();
        loadedLoan.Borrower.ShouldBe("Mario");
        loadedLoan.AccountId.ShouldBe(account.Id);

        var loadedReadings = await readings.ListForLoanAsync(loan.Id);
        loadedReadings.ShouldHaveSingleItem().Amount.ShouldBe(new Money(100m, Euro));
    }

    [PostgresFact]
    public async Task LoanService_ShouldAggregateTheAccountBalanceFromAllLoans()
    {
        var account = await AddLoansAccountAsync();
        var service = BuildService();

        await service.CreateAsync(new NewLoan(
            account.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 10, 1)));
        await service.CreateAsync(new NewLoan(
            account.Id, "Lucia", new Money(50m, Euro), new DateOnly(2026, 10, 5)));
        ClearTracker();

        var latest = (await new ManualBalanceRepository(Context).GetLatestAsync(account.Id))
            .ShouldNotBeNull();
        latest.Amount.ShouldBe(new Money(150m, Euro));

        var overview = await new AccountOverviewService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            new AccountPostingRepository(Context)).ListAsync();
        overview.Single(row => row.AccountId == account.Id)
            .ManualBalance.ShouldBe(new Money(150m, Euro));
    }

    [PostgresFact]
    public async Task DeletingALoan_ShouldRemoveItsReadingsAndUpdateTheAggregate()
    {
        var account = await AddLoansAccountAsync();
        var service = BuildService();
        var mario = await service.CreateAsync(new NewLoan(
            account.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 10, 1)));
        await service.CreateAsync(new NewLoan(
            account.Id, "Lucia", new Money(50m, Euro), new DateOnly(2026, 10, 5)));

        await service.DeleteLoanAsync(mario.Id);
        ClearTracker();

        (await Context.Loans.CountAsync()).ShouldBe(1);
        (await Context.LoanReadings.CountAsync()).ShouldBe(1);
        (await new ManualBalanceRepository(Context).GetLatestAsync(account.Id))
            .ShouldNotBeNull().Amount.ShouldBe(new Money(50m, Euro));
    }

    private LoanService BuildService() =>
        new(
            new LoanRepository(Context),
            new LoanReadingRepository(Context),
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            () => Today);

    private async Task<Account> AddLoansAccountAsync()
    {
        var account = new Account(
            Guid.NewGuid(),
            "Prestiti",
            AccountType.Loans,
            Euro,
            AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(account);
        return account;
    }
}
