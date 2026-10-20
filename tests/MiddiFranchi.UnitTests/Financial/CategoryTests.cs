using MiddiFranchi.Domain.Financial;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class CategoryTests
{
    [Fact]
    public void Construction_ShouldAcceptValidCategories()
    {
        var category = new Category(
            Guid.Parse("08b56884-1c9a-4785-b276-a26a97a5f668"),
            "Alimentari",
            CategoryType.Expense);

        category.Name.ShouldBe("Alimentari");
        category.Type.ShouldBe(CategoryType.Expense);
        category.Description.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construction_ShouldRejectABlankName(string? name)
    {
        Should.Throw<ArgumentException>(
            () => new Category(Guid.NewGuid(), name!, CategoryType.Income))
            .ParamName.ShouldBe("name");
    }
}
