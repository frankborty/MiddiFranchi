using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class OperationUpdateServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly OperationUpdateService _service;

    public OperationUpdateServiceTests()
    {
        _service = new OperationUpdateService(_operations, _categories);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReplaceCategoryAmountDateAndDescription()
    {
        var oldCategory = new Category(Guid.NewGuid(), "Alimentazione", CategoryType.Expense, null, "Spesa");
        var newCategory = new Category(Guid.NewGuid(), "Trasporti", CategoryType.Expense, null, "Pedaggio");
        _categories.Categories.Add(oldCategory);
        _categories.Categories.Add(newCategory);
        var expense = new Expense(
            Guid.NewGuid(),
            account: null,
            oldCategory,
            new PositiveMoney(new Money(10m, Euro)),
            new DateOnly(2026, 4, 10),
            DateTimeOffset.UtcNow,
            "Vecchia descrizione");
        _operations.Operations.Add(expense);

        await _service.UpdateAsync(new UpdateOperationRequest(
            expense.Id,
            newCategory.Id,
            new PositiveMoney(new Money(25.50m, Euro)),
            new DateOnly(2026, 4, 12),
            "Nuova descrizione"));

        var updated = _operations.Operations.ShouldHaveSingleItem().ShouldBeOfType<Expense>();
        updated.Id.ShouldBe(expense.Id);
        updated.Account.ShouldBeNull();
        updated.Category.ShouldBe(newCategory);
        updated.Amount.Value.ShouldBe(new Money(25.50m, Euro));
        updated.OperationDate.ShouldBe(new DateOnly(2026, 4, 12));
        updated.Description.ShouldBe("Nuova descrizione");
        updated.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAnIncompatibleCategoryType()
    {
        var expenseCategory = new Category(Guid.NewGuid(), "Spesa", CategoryType.Expense, null, "Alimentari");
        var incomeCategory = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _categories.Categories.Add(expenseCategory);
        _categories.Categories.Add(incomeCategory);
        var expense = new Expense(
            Guid.NewGuid(),
            account: null,
            expenseCategory,
            new PositiveMoney(new Money(10m, Euro)),
            new DateOnly(2026, 4, 10),
            DateTimeOffset.UtcNow);
        _operations.Operations.Add(expense);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UpdateAsync(new UpdateOperationRequest(
                expense.Id,
                incomeCategory.Id,
                new PositiveMoney(new Money(10m, Euro)),
                new DateOnly(2026, 4, 10))));

        exception.Message.ShouldContain("non è compatibile");
    }

    [Fact]
    public async Task UpdateAsync_ShouldRejectAMissingOperation()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UpdateAsync(new UpdateOperationRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new PositiveMoney(new Money(10m, Euro)),
                new DateOnly(2026, 4, 10))));

        exception.Message.ShouldContain("Operazione non trovata");
    }

    [Fact]
    public async Task UpdateAsync_ShouldFlowTheCancellationToken()
    {
        var category = new Category(Guid.NewGuid(), "Spesa", CategoryType.Expense, null, "Alimentari");
        _categories.Categories.Add(category);
        var expense = new Expense(
            Guid.NewGuid(),
            account: null,
            category,
            new PositiveMoney(new Money(10m, Euro)),
            new DateOnly(2026, 4, 10),
            DateTimeOffset.UtcNow);
        _operations.Operations.Add(expense);
        using var cts = new CancellationTokenSource();

        await _service.UpdateAsync(
            new UpdateOperationRequest(
                expense.Id,
                category.Id,
                new PositiveMoney(new Money(12m, Euro)),
                new DateOnly(2026, 4, 11)),
            cts.Token);

        _operations.ReceivedTokens.ShouldContain(cts.Token);
    }
}
