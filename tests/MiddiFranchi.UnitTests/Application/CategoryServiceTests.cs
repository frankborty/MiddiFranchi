using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class CategoryServiceTests
{
    private readonly FakeCategoryRepository _categories = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_categories);
    }

    [Fact]
    public async Task Create_ShouldPersistANewCategory()
    {
        var request = new NewCategory("Spese casa", CategoryType.Expense, "Per la casa");

        var created = await _service.CreateAsync(request);

        created.Name.ShouldBe("Spese casa");
        created.Type.ShouldBe(CategoryType.Expense);
        created.Description.ShouldBe("Per la casa");
        _categories.Categories.ShouldHaveSingleItem().ShouldBe(created);
        created.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_ShouldRejectAnEmptyName()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync(new NewCategory("", CategoryType.Income, null)));

        exception.Message.ShouldContain("Il nome della categoria è obbligatorio");
    }

    [Fact]
    public async Task GetById_ShouldReturnNullForAMissingCategory()
    {
        var loaded = await _service.GetByIdAsync(Guid.NewGuid());

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task List_ShouldReturnCategoriesInDeterministicOrder()
    {
        await _service.CreateAsync(new NewCategory("Spese casa", CategoryType.Expense, null));
        await _service.CreateAsync(new NewCategory("Stipendio", CategoryType.Income, null));

        var listed = await _service.ListAsync();

        listed.Select(category => category.Name).ShouldBe(["Spese casa", "Stipendio"]);
    }

    [Fact]
    public async Task List_ShouldFlowTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _service.ListAsync(cts.Token);

        _categories.ReceivedTokens.ShouldContain(cts.Token);
    }
}
