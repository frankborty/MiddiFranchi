using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class LoanServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeManualBalanceRepository _manualBalances = new();
    private readonly FakeLoanRepository _loans = new();
    private readonly FakeLoanReadingRepository _readings = new();
    private readonly Account _loansAccount;
    private readonly Account _checkingAccount;
    private readonly LoanService _service;

    public LoanServiceTests()
    {
        _loansAccount = new Account(
            Guid.NewGuid(), "Prestiti", AccountType.Loans, Euro, AccountStatus.Active);
        _checkingAccount = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.AddRange([_loansAccount, _checkingAccount]);

        _service = new LoanService(
            _loans,
            _readings,
            _accounts,
            _manualBalances,
            () => Today);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateTheLoanWithItsInitialReadingAndAggregateTheAccount()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id,
            "Mario",
            new Money(100m, Euro),
            new DateOnly(2026, 10, 1)));

        loan.Borrower.ShouldBe("Mario");
        loan.AccountId.ShouldBe(_loansAccount.Id);
        _loans.Loans.ShouldHaveSingleItem().ShouldBe(loan);
        _readings.Readings.ShouldHaveSingleItem().Amount.ShouldBe(new Money(100m, Euro));

        var aggregate = ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id);
        aggregate.ShouldNotBeNull().Amount.ShouldBe(new Money(100m, Euro));
        _manualBalances.Readings.ShouldAllBe(reading => reading.AccountId == _loansAccount.Id);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectANonLoansAccount()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.CreateAsync(new NewLoan(
                _checkingAccount.Id,
                "Mario",
                new Money(100m, Euro),
                Today)));

        exception.Message.ShouldContain("non è un conto Prestiti");
        _loans.Loans.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ShouldRequireTheDateWhenAnInitialAmountIsProvided()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync(new NewLoan(
                _loansAccount.Id,
                "Mario",
                new Money(100m, Euro),
                null)));

        exception.Message.ShouldContain("data");
        _loans.Loans.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenameAsync_ShouldUpdateTheBorrowerAndKeepTheIdentity()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));

        var renamed = await _service.RenameAsync(loan.Id, "  Mario Rossi  ");

        renamed.Id.ShouldBe(loan.Id);
        renamed.Borrower.ShouldBe("Mario Rossi");
        _loans.Loans.ShouldHaveSingleItem().Borrower.ShouldBe("Mario Rossi");
    }

    [Fact]
    public async Task RenameAsync_ShouldRejectAnEmptyName()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", null, null));

        await Should.ThrowAsync<ArgumentException>(
            () => _service.RenameAsync(loan.Id, "   "));

        _loans.Loans.ShouldHaveSingleItem().Borrower.ShouldBe("Mario");
    }

    [Fact]
    public async Task RegisterRepaymentAsync_ShouldReduceTheOutstandingAndRebuildTheAggregate()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));

        var reading = await _service.RegisterRepaymentAsync(
            loan.Id, new Money(40m, Euro), new DateOnly(2026, 10, 1), "  Acconto  ");

        reading.Amount.ShouldBe(new Money(60m, Euro));
        reading.Note.ShouldBe("Acconto");
        reading.ValidAt.ShouldBe(new DateOnly(2026, 10, 1));
        _readings.Readings.Count.ShouldBe(2);
        ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id)!
            .Amount.ShouldBe(new Money(60m, Euro));
        var overview = await _service.GetAsync(loan.Id);
        overview.ShouldNotBeNull().CurrentAmount.ShouldBe(new Money(60m, Euro));
    }

    [Fact]
    public async Task UpdateReadingAsync_ShouldPreserveTheNote()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));
        var repayment = await _service.RegisterRepaymentAsync(
            loan.Id, new Money(40m, Euro), new DateOnly(2026, 10, 1), "Acconto");

        var updated = await _service.UpdateReadingAsync(
            repayment.Id, new Money(30m, Euro), new DateOnly(2026, 10, 2), "Acconto");

        updated.Amount.ShouldBe(new Money(30m, Euro));
        updated.Note.ShouldBe("Acconto");
    }

    [Fact]
    public async Task RegisterRepaymentAsync_ShouldRejectAnAmountAboveTheOutstanding()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.RegisterRepaymentAsync(loan.Id, new Money(101m, Euro), Today));

        exception.Message.ShouldContain("supera");
        _readings.Readings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RegisterRepaymentAsync_ShouldRequireAReadingAtTheDate()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", null, null));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.RegisterRepaymentAsync(loan.Id, new Money(10m, Euro), Today));

        exception.Message.ShouldContain("rilevazioni");
        _readings.Readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterRepaymentAsync_ShouldRejectFutureDates()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.RegisterRepaymentAsync(loan.Id, new Money(10m, Euro), Today.AddDays(1)));

        exception.Message.ShouldContain("successiva alla data corrente");
        _readings.Readings.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AddReadingAsync_ShouldAggregateTheLatestReadingOfEachLoan()
    {
        var mario = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));
        var lucia = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Lucia", new Money(50m, Euro), new DateOnly(2026, 9, 1)));

        await _service.AddReadingAsync(mario.Id, new Money(120m, Euro), new DateOnly(2026, 10, 1));
        await _service.AddReadingAsync(lucia.Id, new Money(40m, Euro), new DateOnly(2026, 10, 5));

        var aggregate = ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id);
        aggregate.ShouldNotBeNull();
        aggregate.Amount.ShouldBe(new Money(160m, Euro));
        aggregate.ValidAt.ShouldBe(Today);

        var overview = await _service.ListForAccountAsync(_loansAccount.Id);
        overview.Count.ShouldBe(2);
        var marioOverview = overview.Single(item => item.Loan.Id == mario.Id);
        marioOverview.CurrentAmount.ShouldBe(new Money(120m, Euro));
        marioOverview.LatestDate.ShouldBe(new DateOnly(2026, 10, 1));
        marioOverview.Readings.Count.ShouldBe(2);
        var luciaOverview = overview.Single(item => item.Loan.Id == lucia.Id);
        luciaOverview.CurrentAmount.ShouldBe(new Money(40m, Euro));
        luciaOverview.LatestDate.ShouldBe(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public async Task UpdateAndDeleteReading_ShouldRebuildTheAggregate()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));
        var reading = _readings.Readings.ShouldHaveSingleItem();

        var updated = await _service.UpdateReadingAsync(
            reading.Id, new Money(80m, Euro), new DateOnly(2026, 9, 15), null);
        ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id)!
            .Amount.ShouldBe(new Money(80m, Euro));
        updated.RecordedAt.ShouldBe(reading.RecordedAt);

        await _service.DeleteReadingAsync(reading.Id);
        ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id)!
            .Amount.ShouldBe(new Money(0m, Euro));
        loan.Borrower.ShouldBe("Mario");
    }

    [Fact]
    public async Task DeleteLoanAsync_ShouldRemoveTheLoanAndItsContribution()
    {
        var mario = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 9, 1)));
        await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Lucia", new Money(50m, Euro), new DateOnly(2026, 9, 1)));

        await _service.DeleteLoanAsync(mario.Id);

        _loans.Loans.ShouldHaveSingleItem().Borrower.ShouldBe("Lucia");
        ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id)!
            .Amount.ShouldBe(new Money(50m, Euro));
    }

    [Fact]
    public async Task ReadingDates_ShouldRejectFutureDates()
    {
        var loan = await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", null, null));

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.AddReadingAsync(loan.Id, new Money(10m, Euro), Today.AddDays(1)));

        exception.Message.ShouldContain("successiva alla data corrente");
        _readings.Readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task RebuildAllAsync_ShouldRebuildEveryLoansAccount()
    {
        var secondLoansAccount = new Account(
            Guid.NewGuid(), "Altri prestiti", AccountType.Loans, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(secondLoansAccount);
        await _service.CreateAsync(new NewLoan(
            _loansAccount.Id, "Mario", new Money(100m, Euro), new DateOnly(2026, 10, 1)));
        await _service.CreateAsync(new NewLoan(
            secondLoansAccount.Id, "Lucia", new Money(50m, Euro), new DateOnly(2026, 10, 1)));

        // Simula un riavvio: gli aggregati materializzati vengono azzerati e ricostruiti.
        _manualBalances.Readings.Clear();
        await _service.RebuildAllAsync();

        ManualBalances.Latest(_manualBalances.Readings, _loansAccount.Id)!
            .Amount.ShouldBe(new Money(100m, Euro));
        ManualBalances.Latest(_manualBalances.Readings, secondLoansAccount.Id)!
            .Amount.ShouldBe(new Money(50m, Euro));
    }

    [Fact]
    public void LoanOverview_ShouldBeInactiveOnlyWhenTheCurrentAmountIsZero()
    {
        var loan = new Loan(
            Guid.NewGuid(), _loansAccount, "Mario", DateTimeOffset.UtcNow);

        new LoanOverview(loan, null, null, []).IsActive.ShouldBeTrue();
        new LoanOverview(loan, new Money(0m, Euro), Today, []).IsActive.ShouldBeFalse();
        new LoanOverview(loan, new Money(10m, Euro), Today, []).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ListForAccountAsync_ShouldRejectANonLoansAccount()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ListForAccountAsync(_checkingAccount.Id));

        exception.Message.ShouldContain("non è un conto Prestiti");
    }
}
