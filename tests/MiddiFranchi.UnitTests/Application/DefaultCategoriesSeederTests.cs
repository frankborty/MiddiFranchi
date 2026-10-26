using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class DefaultCategoriesSeederTests
{
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeAppMetadataRepository _metadata = new();
    private readonly DefaultCategoriesSeeder _seeder;

    public DefaultCategoriesSeederTests()
    {
        _seeder = new DefaultCategoriesSeeder(_categories, _metadata);
    }

    [Fact]
    public async Task Ensure_ShouldAddAllDefaults_WhenRepositoryIsEmpty()
    {
        var added = await _seeder.EnsureAsync();

        added.ShouldBe(DefaultCategoriesSeeder.Defaults.Count);
        _categories.Categories.Count.ShouldBe(DefaultCategoriesSeeder.Defaults.Count);
        _categories.Categories.ShouldContain(category =>
            category.Name == "Alimentazione" &&
            category.Subcategory == "Spesa" &&
            category.Type == CategoryType.Expense &&
            category.Icon == "utensils" &&
            category.ColorHex == "#22c55e");
        _categories.Categories.ShouldContain(category =>
            category.Name == "Stipendio" &&
            category.Subcategory == null &&
            category.Type == CategoryType.Income &&
            category.Icon == "wallet" &&
            category.ColorHex == "#16a34a");
        _metadata.Values[DefaultCategoriesSeeder.SeedKey].ShouldBe("done");
    }

    [Fact]
    public async Task Ensure_ShouldBeIdempotent()
    {
        await _seeder.EnsureAsync();

        var added = await _seeder.EnsureAsync();

        added.ShouldBe(0);
        _categories.Categories.Count.ShouldBe(DefaultCategoriesSeeder.Defaults.Count);
    }

    [Fact]
    public async Task Ensure_ShouldKeepCustomCategories_AndNotDuplicateExistingDefaults()
    {
        var custom = new Category(Guid.NewGuid(), "Categoria personale", CategoryType.Expense, null, "Custom");
        var existingDefault = new Category(Guid.NewGuid(), "Alimentazione", CategoryType.Expense, null, "Spesa");
        _categories.Categories.Add(custom);
        _categories.Categories.Add(existingDefault);

        var added = await _seeder.EnsureAsync();

        added.ShouldBe(DefaultCategoriesSeeder.Defaults.Count - 1);
        _categories.Categories.ShouldContain(custom);
        _categories.Categories.Count(category =>
            category.Name == "Alimentazione" && category.Subcategory == "Spesa").ShouldBe(1);
    }

    [Fact]
    public async Task Ensure_ShouldSkip_WhenSeedMarkerIsPresent()
    {
        _metadata.Values[DefaultCategoriesSeeder.SeedKey] = "done";
        _metadata.Values[DefaultCategoriesSeeder.InvestmentIncomeSeedKey] = "done";
        _metadata.Values[DefaultCategoriesSeeder.InvestmentFeeSeedKey] = "done";

        var added = await _seeder.EnsureAsync();

        added.ShouldBe(0);
        _categories.Categories.ShouldBeEmpty();
    }

    [Fact]
    public void Defaults_ShouldBeCompleteAndUnique()
    {
        DefaultCategoriesSeeder.Defaults.ShouldAllBe(definition =>
            !string.IsNullOrWhiteSpace(definition.Name));
        DefaultCategoriesSeeder.Defaults
            .Where(definition => definition.Type == CategoryType.Expense)
            .ShouldAllBe(definition => !string.IsNullOrWhiteSpace(definition.Subcategory));
        DefaultCategoriesSeeder.Defaults
            .Where(definition => definition.Type == CategoryType.Income)
            .ShouldAllBe(definition => definition.Subcategory == null);

        DefaultCategoriesSeeder.Defaults
            .Select(definition => (definition.Name, definition.Subcategory, definition.Type))
            .Distinct()
            .Count()
            .ShouldBe(DefaultCategoriesSeeder.Defaults.Count);
    }
}
