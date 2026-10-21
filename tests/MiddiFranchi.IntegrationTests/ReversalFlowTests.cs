using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class ReversalFlowTests : PostgresTestBase
{
    public ReversalFlowTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    private ReversalService BuildReversalService() =>
        new(new FinancialOperationRepository(Context));

    private OperationRegistrationService BuildRegistrationService() =>
        new(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));

    private async Task<(Account Account, Category IncomeCategory, Category ExpenseCategory)> AddOrdinarySetupAsync()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto corrente", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await new AccountRepository(Context).AddAsync(account);
        var incomeCategory = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        var expenseCategory = new Category(Guid.NewGuid(), "Affitto", CategoryType.Expense);
        await new CategoryRepository(Context).AddAsync(incomeCategory);
        await new CategoryRepository(Context).AddAsync(expenseCategory);
        return (account, incomeCategory, expenseCategory);
    }

    private async Task AddManualReadingAsync(Account account, decimal amount)
    {
        await new ManualBalanceRepository(Context).AddAsync(new ManualBalance(
            Guid.NewGuid(), account,
            new Money(amount, account.Currency),
            new DateOnly(2026, 4, 1),
            new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero)));
        ClearTracker();
    }

    [PostgresFact]
    public async Task ReversingAnIncome_ShouldPersistTheReversalWithNoPostingsAndKeepTheOriginalRow()
    {
        var (account, incomeCategory, _) = await AddOrdinarySetupAsync();
        await AddManualReadingAsync(account, 500m);
        var registrations = BuildRegistrationService();
        var income = await registrations.IncomeAsync(new RegisterIncomeRequest(
            account.Id, incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));

        var reversal = await BuildReversalService().ReverseAsync(income.Id);
        ClearTracker();

        var operations = new FinancialOperationRepository(Context);
        var loadedReversal = (await operations.GetByIdAsync(reversal.Id)).ShouldBeOfType<ReversalOperation>();
        loadedReversal.OriginalOperationId.ShouldBe(income.Id);
        (await Context.AccountPostings.CountAsync(postingRecord => postingRecord.FinancialOperationId == reversal.Id))
            .ShouldBe(0);
        // La riga dell'originale resta, con il suo posting fisicamente non cancellato.
        (await Context.AccountPostings.CountAsync(postingRecord => postingRecord.FinancialOperationId == income.Id))
            .ShouldBe(1);
        // La vista economica corrente esclude l'originale stornata.
        (await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, new DateOnly(2026, 5, 1)))
            .ShouldBeEmpty();
        var accountService = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context));
        var calculatedBalance = await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 5, 1));
        calculatedBalance.Amount.ShouldBe(0m);
        // Le rilevazioni manuali non sono toccate.
        (await Context.ManualBalances.CountAsync(readingRecord => readingRecord.AccountId == account.Id))
            .ShouldBe(1);
        (await new ManualBalanceRepository(Context).GetLatestAsync(account.Id))
            .ShouldNotBeNull().Amount.Amount.ShouldBe(500m);
    }

    [PostgresFact]
    public async Task ReversingAnExpense_ShouldCompensateItsNegativePosting()
    {
        var (account, _, expenseCategory) = await AddOrdinarySetupAsync();
        var registrations = BuildRegistrationService();
        var expense = await registrations.ExpenseAsync(new RegisterExpenseRequest(
            account.Id, expenseCategory.Id,
            new PositiveMoney(new Money(30m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));

        var accountService = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context));
        (await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 5, 1)))
            .Amount.ShouldBe(-30m);

        await BuildReversalService().ReverseAsync(expense.Id);
        ClearTracker();

        (await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 5, 1)))
            .Amount.ShouldBe(0m);
    }

    [PostgresFact]
    public async Task ReversingATransfer_ShouldCompensateBothLegs()
    {
        var accountRepository = new AccountRepository(Context);
        var source = new Account(
            Guid.NewGuid(), "Conto EUR", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var destination = new Account(
            Guid.NewGuid(), "Conto USD", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        await accountRepository.AddAsync(source);
        await accountRepository.AddAsync(destination);
        var registrations = new OperationRegistrationService(
            new AccountRepository(Context),
            new CategoryRepository(Context),
            new FinancialOperationRepository(Context));
        var transfer = await registrations.TransferAsync(new RegisterTransferRequest(
            source.Id, destination.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new DateOnly(2026, 4, 12)));
        ClearTracker();

        var repository = new AccountPostingRepository(Context);
        (await repository.ListAsync(source.Id, null, null)).ShouldHaveSingleItem()
            .SignedAmount.ShouldBe(-100m);
        (await repository.ListAsync(destination.Id, null, null)).ShouldHaveSingleItem()
            .SignedAmount.ShouldBe(100m);

        await BuildReversalService().ReverseAsync(transfer.Id);
        ClearTracker();

        // Entrambe le gambe scompaiono dalla vista corrente; le righe restano nel registro.
        (await repository.ListAsync(source.Id, null, null)).ShouldBeEmpty();
        (await repository.ListAsync(destination.Id, null, null)).ShouldBeEmpty();
        (await Context.AccountPostings.CountAsync(postingRecord => postingRecord.FinancialOperationId == transfer.Id))
            .ShouldBe(2);
        (await Context.AccountPostings.CountAsync(postingRecord => postingRecord.FinancialOperationId != transfer.Id))
            .ShouldBe(0);

        // La riga dell'operazione originale resta consultabile con i suoi importi invariati.
        var loaded = (await new FinancialOperationRepository(Context).GetByIdAsync(transfer.Id))
            .ShouldBeOfType<Transfer>();
        loaded.SourceAmount.Value.Amount.ShouldBe(100m);
        loaded.DestinationAmount.Value.Amount.ShouldBe(100m);
        loaded.Status.ShouldBe(OperationStatus.Reversed);
    }

    [PostgresFact]
    public async Task DoubleReversal_ShouldBeRejectedAndLeaveOneReversalRow()
    {
        var (account, incomeCategory, _) = await AddOrdinarySetupAsync();
        var income = await BuildRegistrationService().IncomeAsync(new RegisterIncomeRequest(
            account.Id, incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));
        var service = BuildReversalService();

        var first = await service.ReverseAsync(income.Id);
        ClearTracker();

        var second = await Should.ThrowAsync<ArgumentException>(
            () => service.ReverseAsync(income.Id));

        // Il guard del dominio scatta prima di toccare il repository: l'originale è stato
        // materializzato con il relativo storno e risulta già stornato.
        second.Message.ShouldContain("già stornata");
        (await Context.FinancialOperations.CountAsync(operationRecord =>
                operationRecord.Kind == OperationKind.Reversal &&
                operationRecord.OriginalOperationId == income.Id))
            .ShouldBe(1);
        (await Context.FinancialOperations.CountAsync(operationRecord => operationRecord.Id == first.Id))
            .ShouldBe(1);
    }

    [PostgresFact]
    public async Task ReversingAReversal_ShouldBeRejected()
    {
        var (account, incomeCategory, _) = await AddOrdinarySetupAsync();
        var income = await BuildRegistrationService().IncomeAsync(new RegisterIncomeRequest(
            account.Id, incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));
        var reversal = await BuildReversalService().ReverseAsync(income.Id);
        ClearTracker();

        var error = await Should.ThrowAsync<ArgumentException>(
            () => BuildReversalService().ReverseAsync(reversal.Id));

        error.Message.ShouldContain("non può essere stornato");
        (await Context.FinancialOperations.CountAsync(operationRecord =>
                operationRecord.OriginalOperationId == reversal.Id))
            .ShouldBe(0);
    }

    [PostgresFact]
    public async Task ReversingAMissingOperation_ShouldBeRejected()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => BuildReversalService().ReverseAsync(Guid.NewGuid()));

        error.Message.ShouldContain("non esiste");
    }

    [PostgresFact]
    public async Task TwoSimultaneousReversalRequests_ShouldNotProduceTwoReversalRows()
    {
        // Simula due richieste di storno sulla stessa operazione. La guardia del repository
        // e l'indice univoco su OriginalOperationId garantiscono un solo storno anche in
        // un'eventuale corsa reale tra due connessioni diverse.
        var (account, incomeCategory, _) = await AddOrdinarySetupAsync();
        var income = await BuildRegistrationService().IncomeAsync(new RegisterIncomeRequest(
            account.Id, incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));
        ClearTracker();

        var transaction = Context.Database.CurrentTransaction!.GetDbTransaction();
        var parallelContext = new MiddiFranchiDbContext(
            new DbContextOptionsBuilder<MiddiFranchiDbContext>()
                .UseNpgsql(Context.Database.GetDbConnection())
                .Options);
        parallelContext.Database.UseTransaction(transaction);
        var parallelService = new ReversalService(new FinancialOperationRepository(parallelContext));

        await BuildReversalService().ReverseAsync(income.Id);
        var parallelAttempt = await Should.ThrowAsync<ArgumentException>(
            () => parallelService.ReverseAsync(income.Id));
        parallelAttempt.Message.ShouldContain("già stornata");
        parallelContext.Dispose();
        ClearTracker();

        (await Context.FinancialOperations.CountAsync(operationRecord =>
                operationRecord.Kind == OperationKind.Reversal &&
                operationRecord.OriginalOperationId == income.Id))
            .ShouldBe(1);
        (await new FinancialOperationRepository(Context).GetByIdAsync(income.Id))
            .ShouldNotBeNull().Status.ShouldBe(OperationStatus.Reversed);
    }

    [PostgresFact]
    public async Task ReversedOperations_ShouldBeExcludedFromHistoricalRebuildsToo()
    {
        // Nella prima versione la vista ricostruita a una data passata include solo le
        // operazioni attuali valide: uno storno corrente cambia anche la ricostruzione
        // di date precedenti, per la regola documentata del dominio (append-only).
        var (account, incomeCategory, _) = await AddOrdinarySetupAsync();
        var income = await BuildRegistrationService().IncomeAsync(new RegisterIncomeRequest(
            account.Id, incomeCategory.Id,
            new PositiveMoney(new Money(120m, Currency.Euro)),
            new DateOnly(2026, 4, 10)));
        await BuildReversalService().ReverseAsync(income.Id);
        ClearTracker();

        var view = await new AccountPostingRepository(Context).ListForAccountAsync(
            account.Id, new DateOnly(2026, 4, 10));

        view.ShouldBeEmpty();
        var accountService = new AccountService(
            new AccountRepository(Context),
            new AccountPostingRepository(Context));
        (await accountService.GetCalculatedBalanceAsync(account.Id, new DateOnly(2026, 4, 10)))
            .Amount.ShouldBe(0m);
    }
}
