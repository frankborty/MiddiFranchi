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
    public async Task IncomeAsync_ShouldPersistAnIncomeWithoutAnAccountOrPostings()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _categories.Categories.Add(category);
        var request = new RegisterIncomeRequest(
            category.Id, new PositiveMoney(new Money(100m, Euro)),
            OperationDate, "Stipendio marzo");

        var income = await _service.IncomeAsync(request);

        income.Account.ShouldBeNull();
        income.Category.ShouldBe(category);
        income.Amount.Value.ShouldBe(new Money(100m, Euro));
        income.OperationDate.ShouldBe(OperationDate);
        income.Description.ShouldBe("Stipendio marzo");
        income.Status.ShouldBe(OperationStatus.Registered);
        income.RecordedAt.ShouldNotBe(default(DateTimeOffset));
        var operation = _operations.Operations.ShouldHaveSingleItem();
        income.ShouldBe(operation);
        income.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public async Task IncomeAsync_ShouldRejectAMissingCategory()
    {
        var request = new RegisterIncomeRequest(
            Guid.NewGuid(), new PositiveMoney(new Money(100m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.IncomeAsync(request));

        exception.Message.ShouldContain("La categoria");
    }

    [Fact]
    public async Task IncomeAsync_ShouldRejectAnExpenseCategory()
    {
        var category = new Category(Guid.NewGuid(), "Spese casa", CategoryType.Expense, null);
        _categories.Categories.Add(category);
        var request = new RegisterIncomeRequest(
            category.Id, new PositiveMoney(new Money(100m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.IncomeAsync(request));

        exception.Message.ShouldContain("non è di tipo Income");
    }

    [Fact]
    public async Task ExpenseAsync_ShouldPersistAnExpenseWithoutAnAccountOrPostings()
    {
        var category = new Category(Guid.NewGuid(), "Spese casa", CategoryType.Expense, null);
        _categories.Categories.Add(category);
        var request = new RegisterExpenseRequest(
            category.Id, new PositiveMoney(new Money(30m, Euro)), OperationDate);

        var expense = await _service.ExpenseAsync(request);

        expense.Kind.ShouldBe(OperationKind.Expense);
        expense.Account.ShouldBeNull();
        _operations.Operations.ShouldHaveSingleItem().ShouldBe(expense);
        expense.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExpenseAsync_ShouldRejectAnIncomeCategory()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _categories.Categories.Add(category);
        var request = new RegisterExpenseRequest(
            category.Id, new PositiveMoney(new Money(30m, Euro)), OperationDate);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ExpenseAsync(request));

        exception.Message.ShouldContain("non è di tipo Expense");
    }
}
