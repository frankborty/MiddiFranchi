using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class FinancialOperationRepositoryTests : PostgresTestBase
{
    public FinancialOperationRepositoryTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Income_ShouldRoundTripAndGenerateItsPosting()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var repository = new FinancialOperationRepository(Context);
        var amount = new PositiveMoney(new Money(123.456789012345678m, Currency.Euro));
        var date = new DateOnly(2026, 3, 10);
        var recordedAt = new DateTimeOffset(2026, 3, 10, 12, 30, 45, TimeSpan.Zero);
        var income = new Income(
            Guid.NewGuid(), account, category, amount, date, recordedAt, "Stipendio", "Nota");

        await repository.AddAsync(income);
        ClearTracker();

        var loaded = await repository.GetByIdAsync(income.Id);

        var loadedIncome = loaded.ShouldBeOfType<Income>();
        loadedIncome.Id.ShouldBe(income.Id);
        loadedIncome.Account.Id.ShouldBe(account.Id);
        loadedIncome.Category.Id.ShouldBe(category.Id);
        loadedIncome.Amount.Value.Amount.ShouldBe(amount.Value.Amount);
        loadedIncome.Amount.Value.Currency.ShouldBe(amount.Value.Currency);
        loadedIncome.OperationDate.ShouldBe(date);
        loadedIncome.RecordedAt.ShouldBe(recordedAt);
        loadedIncome.Description.ShouldBe("Stipendio");
        loadedIncome.Notes.ShouldBe("Nota");
        loadedIncome.Status.ShouldBe(OperationStatus.Registered);

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        var posting = postings.ShouldHaveSingleItem();
        posting.SignedAmount.ShouldBe(amount.Value.Amount);
        posting.Kind.ShouldBe(PostingKind.Income);
        posting.Currency.ShouldBe(amount.Value.Currency);
        posting.Operation.Id.ShouldBe(income.Id);
        AccountBalanceCalculator.Balance(account, postings, date).ShouldBe(amount.Value);
    }

    [PostgresFact]
    public async Task Expense_ShouldRoundTripAndGenerateItsPosting()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Expense);
        var repository = new FinancialOperationRepository(Context);
        var amount = new PositiveMoney(new Money(42.50m, Currency.Euro));
        var date = new DateOnly(2026, 3, 11);
        var expense = new Expense(
            Guid.NewGuid(),
            account,
            category,
            amount,
            date,
            new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(expense);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(expense.Id)).ShouldBeOfType<Expense>();
        loaded.Amount.Value.ShouldBe(amount.Value);
        loaded.Account.Id.ShouldBe(account.Id);
        loaded.Category.Id.ShouldBe(category.Id);

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        postings.ShouldHaveSingleItem().SignedAmount.ShouldBe(-amount.Value.Amount);
    }

    [PostgresFact]
    public async Task Transfer_ShouldRoundTripAndPersistBothLegs()
    {
        var source = new Account(
            Guid.NewGuid(), "Conto EUR", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var destination = new Account(
            Guid.NewGuid(), "Conto USD", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var accountRepository = new AccountRepository(Context);
        await accountRepository.AddAsync(source);
        await accountRepository.AddAsync(destination);

        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 12);
        var transfer = new Transfer(
            Guid.NewGuid(),
            source,
            destination,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            new PositiveMoney(new Money(100m, Currency.Euro)),
            date,
            new DateTimeOffset(2026, 3, 12, 15, 45, 0, TimeSpan.Zero));

        await repository.AddAsync(transfer);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(transfer.Id)).ShouldBeOfType<Transfer>();
        loaded.SourceAccount.Id.ShouldBe(source.Id);
        loaded.DestinationAccount.Id.ShouldBe(destination.Id);
        loaded.SourceAmount.Value.ShouldBe(new Money(100m, Currency.Euro));
        loaded.DestinationAmount.Value.ShouldBe(new Money(100m, Currency.Euro));

        var postingRepository = new AccountPostingRepository(Context);
        var sourcePostings = await postingRepository.ListForAccountAsync(source.Id, date);
        var destinationPostings = await postingRepository.ListForAccountAsync(destination.Id, date);

        var sourcePosting = sourcePostings.ShouldHaveSingleItem();
        sourcePosting.SignedAmount.ShouldBe(-100m);
        sourcePosting.Currency.ShouldBe(Currency.Euro);
        sourcePosting.Operation.Id.ShouldBe(transfer.Id);
        sourcePosting.Kind.ShouldBe(PostingKind.TransferLeg);

        var destinationPosting = destinationPostings.ShouldHaveSingleItem();
        destinationPosting.SignedAmount.ShouldBe(100m);
        destinationPosting.Currency.ShouldBe(Currency.Euro);
        destinationPosting.Operation.Id.ShouldBe(transfer.Id);
        destinationPosting.Kind.ShouldBe(PostingKind.TransferLeg);

        AccountBalanceCalculator.Balance(source, sourcePostings, date)
            .ShouldBe(new Money(-100m, Currency.Euro));
        AccountBalanceCalculator.Balance(destination, destinationPostings, date)
            .ShouldBe(new Money(100m, Currency.Euro));
    }

    [PostgresFact]
    public async Task InvestmentBuy_ShouldRoundTripWithFeesAndPostings()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Euro);
        var instrument = new Instrument(
            Guid.NewGuid(),
            "Obbligazione",
            InstrumentType.Bond,
            Currency.Euro,
            PriceUnit.PercentOfNominal,
            new Money(1000m, Currency.Euro));
        var accountRepository = new AccountRepository(Context);
        var portfolioRepository = new InvestmentPortfolioRepository(Context);
        var instrumentRepository = new InstrumentRepository(Context);
        await accountRepository.AddAsync(account);
        await portfolioRepository.AddAsync(portfolio);
        await instrumentRepository.AddAsync(instrument);

        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 18);
        var fee = new FeeComponent(
            FeeKind.BuyFee,
            new PositiveMoney(new Money(2m, Currency.Euro)),
            account);
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new Quantity(3m),
            QuotedPrice.AsPercentOfNominal(98.5m),
            [fee],
            date,
            new DateTimeOffset(2026, 3, 18, 10, 15, 0, TimeSpan.Zero));

        await repository.AddAsync(operation);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(operation.Id)).ShouldBeOfType<InvestmentOperation>();
        loaded.InvestmentKind.ShouldBe(InvestmentOperationKind.Buy);
        loaded.Portfolio.Id.ShouldBe(portfolio.Id);
        loaded.Instrument.ShouldNotBeNull();
        loaded.Instrument.Id.ShouldBe(instrument.Id);
        loaded.Instrument.NotionalValue.ShouldBe(new Money(1000m, Currency.Euro));
        loaded.SettlementAccount.Id.ShouldBe(account.Id);
        loaded.Quantity.ShouldNotBeNull();
        loaded.Quantity.Value.Value.ShouldBe(3m);
        loaded.QuotedPrice.ShouldNotBeNull();
        loaded.QuotedPrice.Value.PercentOfNominal.ShouldBe(98.5m);
        loaded.UnitPrice.ShouldNotBeNull();
        loaded.UnitPrice.Value.Price.ShouldBe(new Money(985m, Currency.Euro));
        loaded.Fees.Count.ShouldBe(1);
        loaded.Fees[0].Kind.ShouldBe(FeeKind.BuyFee);
        loaded.Fees[0].Amount.Value.ShouldBe(new Money(2m, Currency.Euro));
        loaded.Fees[0].DebitAccount.Id.ShouldBe(account.Id);

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        postings.Count.ShouldBe(2);
        postings.Single(posting => posting.Kind == PostingKind.Capital).SignedAmount.ShouldBe(-2955m);
        postings.Single(posting => posting.Kind == PostingKind.Fee).SignedAmount.ShouldBe(-2m);
        AccountBalanceCalculator.Balance(account, postings, date)
            .ShouldBe(new Money(-2957m, Currency.Euro));
    }

    [PostgresFact]
    public async Task InvestmentFees_ShouldPreserveAmountsCurrenciesAndOrder()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Euro);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF", InstrumentType.Etf, Currency.Euro, PriceUnit.PerUnit);
        await new AccountRepository(Context).AddAsync(account);
        await new InvestmentPortfolioRepository(Context).AddAsync(portfolio);
        await new InstrumentRepository(Context).AddAsync(instrument);

        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 30);
        var firstFee = new FeeComponent(
            FeeKind.BuyFee,
            new PositiveMoney(new Money(2m, Currency.Euro)),
            account);
        var secondFee = new FeeComponent(
            FeeKind.BuyFee,
            new PositiveMoney(new Money(1m, Currency.Euro)),
            account);
        var operation = InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new Quantity(1m),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Currency.Euro))),
            [firstFee, secondFee],
            date,
            new DateTimeOffset(2026, 3, 30, 8, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(operation);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(operation.Id)).ShouldBeOfType<InvestmentOperation>();
        loaded.Fees.Count.ShouldBe(2);
        loaded.Fees.Select(fee => fee.Amount.Value.Amount).ShouldBe([2m, 1m]);
        loaded.Fees.ShouldAllBe(fee => fee.Amount.Value.Currency == Currency.Euro);

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        postings.Count.ShouldBe(3);
        postings.Where(posting => posting.Kind == PostingKind.Fee)
            .Select(posting => posting.SignedAmount)
            .OrderBy(amount => amount)
            .ShouldBe([-2m, -1m]);
        AccountBalanceCalculator.Balance(account, postings, date)
            .ShouldBe(new Money(-83m, Currency.Euro));
    }

    [PostgresFact]
    public async Task Reversal_ShouldKeepTheOriginalRowAndExcludeItsActivePostings()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 12);
        var income = new Income(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            date,
            new DateTimeOffset(2026, 3, 12, 8, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(income);

        var reversal = new ReversalOperation(
            Guid.NewGuid(),
            income,
            date.AddDays(1),
            new DateTimeOffset(2026, 3, 13, 9, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(reversal);
        ClearTracker();

        var loadedOriginal = await repository.GetByIdAsync(income.Id);
        var original = loadedOriginal.ShouldBeOfType<Income>();
        original.Status.ShouldBe(OperationStatus.Reversed);
        original.ReversalOperationId.ShouldBe(reversal.Id);

        var loadedReversal = (await repository.GetByIdAsync(reversal.Id)).ShouldBeOfType<ReversalOperation>();
        loadedReversal.OriginalOperationId.ShouldBe(income.Id);
        loadedReversal.Original.Id.ShouldBe(income.Id);
        loadedReversal.Original.Status.ShouldBe(OperationStatus.Reversed);

        var postings = await new AccountPostingRepository(Context)
            .ListForAccountAsync(account.Id, date.AddDays(1));
        postings.ShouldBeEmpty();
        AccountBalanceCalculator.Balance(account, postings, date.AddDays(1))
            .ShouldBe(new Money(0m, Currency.Euro));

        (await Context.AccountPostings.CountAsync(posting => posting.FinancialOperationId == income.Id))
            .ShouldBe(1);

        var clonedOriginal = new Income(
            income.Id,
            account,
            category,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            date,
            new DateTimeOffset(2026, 3, 12, 8, 0, 0, TimeSpan.Zero));
        var duplicateReversal = new ReversalOperation(
            Guid.NewGuid(),
            clonedOriginal,
            date.AddDays(2),
            new DateTimeOffset(2026, 3, 14, 9, 0, 0, TimeSpan.Zero));

        await Should.ThrowAsync<InvalidOperationException>(() => repository.AddAsync(duplicateReversal));
    }

    [PostgresFact]
    public async Task Dividend_ShouldRoundTripAndGenerateItsIncomePosting()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Euro);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF", InstrumentType.Etf, Currency.Euro, PriceUnit.PerUnit);
        await new AccountRepository(Context).AddAsync(account);
        await new InvestmentPortfolioRepository(Context).AddAsync(portfolio);
        await new InstrumentRepository(Context).AddAsync(instrument);

        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 19);
        var operation = InvestmentOperation.Dividend(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new PositiveMoney(new Money(12.5m, Currency.Euro)),
            date,
            new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(operation);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(operation.Id)).ShouldBeOfType<InvestmentOperation>();
        loaded.InvestmentKind.ShouldBe(InvestmentOperationKind.Dividend);
        loaded.Amount.ShouldNotBeNull();
        loaded.Amount.Value.Value.ShouldBe(new Money(12.5m, Currency.Euro));
        loaded.Instrument!.Id.ShouldBe(instrument.Id);

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        postings.ShouldHaveSingleItem().SignedAmount.ShouldBe(12.5m);
        postings.ShouldHaveSingleItem().Kind.ShouldBe(PostingKind.Income);
    }

    [PostgresFact]
    public async Task StandaloneFee_ShouldRoundTripWithItsFeeComponent()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Euro);
        await new AccountRepository(Context).AddAsync(account);
        await new InvestmentPortfolioRepository(Context).AddAsync(portfolio);

        var repository = new FinancialOperationRepository(Context);
        var date = new DateOnly(2026, 3, 21);
        var operation = InvestmentOperation.StandaloneFee(
            Guid.NewGuid(),
            portfolio,
            account,
            new PositiveMoney(new Money(0.5m, Currency.Euro)),
            date,
            new DateTimeOffset(2026, 3, 21, 10, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(operation);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(operation.Id)).ShouldBeOfType<InvestmentOperation>();
        loaded.InvestmentKind.ShouldBe(InvestmentOperationKind.StandaloneFee);
        loaded.Instrument.ShouldBeNull();
        loaded.Fees.ShouldHaveSingleItem().Kind.ShouldBe(FeeKind.Standalone);
        loaded.Fees[0].Amount.Value.ShouldBe(new Money(0.5m, Currency.Euro));

        var postings = await new AccountPostingRepository(Context).ListForAccountAsync(account.Id, date);
        postings.ShouldHaveSingleItem().SignedAmount.ShouldBe(-0.5m);
        postings.ShouldHaveSingleItem().Kind.ShouldBe(PostingKind.Fee);
    }

    [PostgresFact]
    public async Task List_ShouldReturnAllOperationsIncludingReversalsInDeterministicOrder()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var repository = new FinancialOperationRepository(Context);
        var first = new Income(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(10m, Currency.Euro)),
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero));
        var second = new Income(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(4m, Currency.Euro)),
            new DateOnly(2026, 3, 11),
            new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        var reversal = new ReversalOperation(
            Guid.NewGuid(),
            first,
            new DateOnly(2026, 3, 12),
            new DateTimeOffset(2026, 3, 12, 8, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(reversal);
        ClearTracker();

        var operations = await repository.ListAsync();

        operations.Count.ShouldBe(3);
        operations.Select(operation => operation.Id).ShouldBe([first.Id, second.Id, reversal.Id]);
        operations.Single(operation => operation.Id == first.Id).Status.ShouldBe(OperationStatus.Reversed);
        operations.Single(operation => operation.Id == reversal.Id).ShouldBeOfType<ReversalOperation>();
    }

    [PostgresFact]
    public async Task ListInvestmentsByPortfolio_ShouldFilterByDateAndReversalStatus()
    {
        var account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var portfolio = new InvestmentPortfolio(Guid.NewGuid(), "Portafoglio", Currency.Euro);
        var instrument = new Instrument(
            Guid.NewGuid(), "ETF", InstrumentType.Etf, Currency.Euro, PriceUnit.PerUnit);
        await new AccountRepository(Context).AddAsync(account);
        await new InvestmentPortfolioRepository(Context).AddAsync(portfolio);
        await new InstrumentRepository(Context).AddAsync(instrument);

        var repository = new FinancialOperationRepository(Context);
        var first = InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new Quantity(1m),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(80m, Currency.Euro))),
            null,
            new DateOnly(2026, 3, 10),
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero));
        var second = InvestmentOperation.Buy(
            Guid.NewGuid(),
            portfolio,
            instrument,
            account,
            new Quantity(2m),
            QuotedPrice.AtUnitPrice(new UnitPrice(new Money(90m, Currency.Euro))),
            null,
            new DateOnly(2026, 3, 20),
            new DateTimeOffset(2026, 3, 20, 8, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(first);
        await repository.AddAsync(second);

        var beforeSecond = await repository.ListInvestmentsByPortfolioAsync(portfolio.Id, new DateOnly(2026, 3, 15));
        beforeSecond.ShouldHaveSingleItem().Id.ShouldBe(first.Id);

        await repository.AddAsync(new ReversalOperation(
            Guid.NewGuid(),
            first,
            new DateOnly(2026, 3, 16),
            new DateTimeOffset(2026, 3, 16, 8, 0, 0, TimeSpan.Zero)));

        (await repository.ListInvestmentsByPortfolioAsync(portfolio.Id, new DateOnly(2026, 3, 15)))
            .ShouldBeEmpty();
        var afterSecond = await repository.ListInvestmentsByPortfolioAsync(portfolio.Id, new DateOnly(2026, 3, 25));
        afterSecond.ShouldHaveSingleItem().Id.ShouldBe(second.Id);
    }

    [PostgresFact]
    public async Task GetById_ShouldReturnNullWhenMissing()
    {
        var repository = new FinancialOperationRepository(Context);

        (await repository.GetByIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [PostgresFact]
    public async Task DuplicateOperationId_ShouldRollBackPostingsAndLeaveNoPartialState()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var repository = new FinancialOperationRepository(Context);
        var operationId = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 22);
        var recordedAt = new DateTimeOffset(2026, 3, 22, 8, 0, 0, TimeSpan.Zero);
        var first = new Income(
            operationId,
            account,
            category,
            new PositiveMoney(new Money(10m, Currency.Euro)),
            date,
            recordedAt);
        await repository.AddAsync(first);
        ClearTracker();

        var transaction = Context.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("before_duplicate");
        var duplicate = new Income(
            operationId,
            account,
            category,
            new PositiveMoney(new Money(20m, Currency.Euro)),
            date,
            recordedAt);

        await Should.ThrowAsync<DbUpdateException>(() => repository.AddAsync(duplicate));
        await transaction.RollbackToSavepointAsync("before_duplicate");
        ClearTracker();

        (await Context.FinancialOperations.CountAsync(operation => operation.Id == operationId))
            .ShouldBe(1);
        (await Context.AccountPostings.CountAsync(posting => posting.FinancialOperationId == operationId))
            .ShouldBe(1);
        var loaded = (await repository.GetByIdAsync(operationId)).ShouldBeOfType<Income>();
        loaded.Amount.Value.Amount.ShouldBe(10m);
    }

    [PostgresFact]
    public async Task RecordedAt_ShouldBeStoredWithPostgresMicrosecondPrecision()
    {
        var (account, category) = await AddOrdinaryContextAsync(CategoryType.Income);
        var repository = new FinancialOperationRepository(Context);
        var recordedAt = new DateTimeOffset(2026, 3, 23, 8, 0, 0, TimeSpan.Zero).AddTicks(1236);
        var income = new Income(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(1m, Currency.Euro)),
            new DateOnly(2026, 3, 23),
            recordedAt);

        await repository.AddAsync(income);
        ClearTracker();

        var loaded = (await repository.GetByIdAsync(income.Id)).ShouldBeOfType<Income>();

        (loaded.RecordedAt.Ticks % 10).ShouldBe(0);
        Math.Abs(loaded.RecordedAt.Ticks - recordedAt.Ticks).ShouldBeLessThan(10L);
        loaded.RecordedAt.ShouldNotBe(recordedAt);
    }

    private async Task<(Account Account, Category Category)> AddOrdinaryContextAsync(CategoryType type)
    {
        var account = new Account(
            Guid.NewGuid(), "Conto", AccountType.Checking, Currency.Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Categoria", type);
        await new AccountRepository(Context).AddAsync(account);
        await new CategoryRepository(Context).AddAsync(category);
        return (account, category);
    }
}
