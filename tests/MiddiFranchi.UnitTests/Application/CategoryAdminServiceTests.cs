using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public sealed class CategoryAdminServiceTests
{
    [Fact]
    public async Task DeleteCategoryWithOperationsWithoutDestination_ShouldBeRejected()
    {
        var (service, categories, _) = CreateServiceWithExpenseOperations();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.DeleteCategoryGroupAsync(CategoryType.Expense, "Casa", null));

        categories.Categories.Count(category => category.Name == "Casa").ShouldBe(2);
    }

    [Fact]
    public async Task DeleteCategoryWithOperations_ShouldMoveOperationsAndDeleteRows()
    {
        var (service, categories, operations) = CreateServiceWithExpenseOperations();
        var destination = categories.Categories.Single(category => category.Name == "Auto");

        var moved = await service.DeleteCategoryGroupAsync(
            CategoryType.Expense,
            "Casa",
            destination.Id);

        moved.ShouldBe(3);
        operations.Operations.Count.ShouldBe(3);
        operations.Operations.All(operation =>
            operation is Expense expense && expense.Category.Id == destination.Id).ShouldBeTrue();
        categories.Categories.ShouldNotContain(category => category.Name == "Casa");
    }

    [Fact]
    public async Task MoveCategoryOperations_ShouldReassignAllGroupRows()
    {
        var (service, categories, operations) = CreateServiceWithExpenseOperations();
        var destination = categories.Categories.Single(category => category.Name == "Auto");

        var moved = await service.MoveCategoryOperationsAsync(
            CategoryType.Expense,
            "Casa",
            destination.Id);

        moved.ShouldBe(3);
        operations.Operations.All(operation =>
            operation is Expense expense && expense.Category.Id == destination.Id).ShouldBeTrue();
        categories.Categories.Count(category => category.Name == "Casa").ShouldBe(2);
    }

    [Fact]
    public async Task MoveSubcategoryOperations_ShouldOnlyReassignTheSourceRow()
    {
        var (service, categories, operations) = CreateServiceWithExpenseOperations();
        var source = categories.Categories.Single(category => category.Subcategory == "Affitto");
        var destination = categories.Categories.Single(category => category.Subcategory == "Utenze");

        var moved = await service.MoveSubcategoryOperationsAsync(source.Id, destination.Id);

        moved.ShouldBe(2);
        operations.Operations.Count(operation =>
            operation is Expense expense && expense.Category.Id == destination.Id).ShouldBe(3);
    }

    [Fact]
    public async Task UpdateGroup_ShouldUpdateNameIconAndColorOnEveryRow()
    {
        var (service, categories, _) = CreateServiceWithExpenseOperations();

        await service.UpdateGroupAsync(
            CategoryType.Expense,
            "Casa",
            "Casa nuova",
            "Descrizione",
            "home",
            "#123456");

        var rows = categories.Categories.Where(category => category.Name == "Casa nuova").ToArray();
        rows.Length.ShouldBe(2);
        rows.All(category =>
            category.Icon == "home" &&
            category.ColorHex == "#123456" &&
            category.Description == "Descrizione").ShouldBeTrue();
        categories.Categories.ShouldNotContain(category => category.Name == "Casa");
    }

    [Fact]
    public async Task AddSubcategory_DuplicateName_ShouldBeRejected()
    {
        var (service, categories, _) = CreateServiceWithExpenseOperations();
        var source = categories.Categories.Single(category => category.Subcategory == "Affitto");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.AddSubcategoryAsync(source.Id, "Utenze"));
    }

    private static (CategoryAdminService Service, FakeCategoryRepository Categories, FakeFinancialOperationRepository Operations)
        CreateServiceWithExpenseOperations()
    {
        var categories = new FakeCategoryRepository();
        var operations = new FakeFinancialOperationRepository();
        var rent = new Category(
            Guid.NewGuid(), "Casa", CategoryType.Expense, "Abitazione", "Affitto", "home", "#111111");
        var utilities = new Category(
            Guid.NewGuid(), "Casa", CategoryType.Expense, "Abitazione", "Utenze", "home", "#111111");
        var fuel = new Category(
            Guid.NewGuid(), "Auto", CategoryType.Expense, "Trasporti", "Carburante", "car", "#222222");
        categories.Categories.AddRange([rent, utilities, fuel]);

        operations.Operations.AddRange(
        [
            new Expense(
                Guid.NewGuid(),
                null,
                rent,
                new PositiveMoney(new Money(700m, Currency.Euro)),
                new DateOnly(2026, 1, 1),
                DateTimeOffset.UtcNow,
                "Affitto"),
            new Expense(
                Guid.NewGuid(),
                null,
                rent,
                new PositiveMoney(new Money(100m, Currency.Euro)),
                new DateOnly(2026, 2, 1),
                DateTimeOffset.UtcNow,
                "Affitto"),
            new Expense(
                Guid.NewGuid(),
                null,
                utilities,
                new PositiveMoney(new Money(50m, Currency.Euro)),
                new DateOnly(2026, 3, 1),
                DateTimeOffset.UtcNow,
                "Utenze"),
        ]);

        return (new CategoryAdminService(categories, operations), categories, operations);
    }
}
