using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class OperationRegistrationServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly OperationDate = new(2026, 3, 10);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly OperationRegistrationService _service;

    public OperationRegistrationServiceTests()
    {
        _service = new OperationRegistrationService(_accounts, _categories, _operations);
    }

    [Fact]
    public async Task IncomeAsync_ShouldPersistAnIncomeWithItsPositivePosting()
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);
        var request = new RegisterIncomeRequest(
            account.Id, category.Id, new PositiveMoney(new Money(100m, Euro)),
            OperationDate, "Stipendio marzo", "nota");

        var income = await _service.IncomeAsync(request);

        income.Account.ShouldBe(account);
        income.Category.ShouldBe(category);
        income.Amount.Value.ShouldBe(new Money(100m, Euro));
        income.OperationDate.ShouldBe(OperationDate);
        income.Description.ShouldBe("Stipendio marzo");
        income.Notes.ShouldBe("nota");
        income.Status.ShouldBe(OperationStatus.Registered);
        income.RecordedAt.ShouldNotBe(default(DateTimeOffset));
        var operation = _operations.Operations.ShouldHaveSingleItem();
        income.ShouldBe(operation);
        var posting = income.GeneratePostings().ShouldHaveSingleItem();
        posting.AccountId.ShouldBe(account.Id);
        posting.Kind.ShouldBe(PostingKind.Income);
        posting.SignedAmount.ShouldBe(100m);
        posting.Currency.ShouldBe(Euro);
    }

    [Fact]
    public async Task IncomeAsync_ShouldRejectAMissingAccount()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _categories.Categories.Add(category);
        var request = new RegisterIncomeRequest(
            Guid.NewGuid(), category.Id, new PositiveMoney(new Money(100m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.IncomeAsync(request));

        exception.Message.ShouldContain("Il conto");
    }

    [Fact]
    public async Task IncomeAsync_ShouldRejectAMissingCategory()
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        var request = new RegisterIncomeRequest(
            account.Id, Guid.NewGuid(), new PositiveMoney(new Money(100m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.IncomeAsync(request));

        exception.Message.ShouldContain("La categoria");
    }

    [Fact]
    public async Task IncomeAsync_ShouldRejectAnExpenseCategory()
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Spese casa", CategoryType.Expense, null);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);
        var request = new RegisterIncomeRequest(
            account.Id, category.Id, new PositiveMoney(new Money(100m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.IncomeAsync(request));

        exception.Message.ShouldContain("non è di tipo Income");
    }

    [Fact]
    public async Task ExpenseAsync_ShouldPersistAnExpenseWithItsNegativeCapitalPosting()
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Spese casa", CategoryType.Expense, null);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);
        var request = new RegisterExpenseRequest(
            account.Id, category.Id, new PositiveMoney(new Money(30m, Euro)), OperationDate);

        var expense = await _service.ExpenseAsync(request);

        expense.Kind.ShouldBe(OperationKind.Expense);
        _operations.Operations.ShouldHaveSingleItem().ShouldBe(expense);
        var posting = expense.GeneratePostings().ShouldHaveSingleItem();
        posting.Kind.ShouldBe(PostingKind.Capital);
        posting.SignedAmount.ShouldBe(-30m);
        posting.Currency.ShouldBe(Euro);
    }

    [Fact]
    public async Task ExpenseAsync_ShouldRejectAnIncomeCategory()
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);
        var request = new RegisterExpenseRequest(
            account.Id, category.Id, new PositiveMoney(new Money(30m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ExpenseAsync(request));

        exception.Message.ShouldContain("non è di tipo Expense");
    }
}
