using MiddiFranchi.Application.Financial;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public sealed class NameUniquenessTests
{
    [Fact]
    public void UniqueName_ShouldReturnNull()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate("Revolut", ["BBVA", "PostePay"]);

        suggestion.ShouldBeNull();
    }

    [Fact]
    public void DuplicateName_ShouldSuggestTheNextProgressiveName()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate("PostePay", ["BBVA", "PostePay"]);

        suggestion.ShouldBe("PostePay (2)");
    }

    [Fact]
    public void DuplicateName_ShouldSkipAlreadyUsedProgressiveNames()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate(
            "PostePay",
            ["PostePay", "PostePay (2)", "PostePay (3)"]);

        suggestion.ShouldBe("PostePay (4)");
    }

    [Fact]
    public void ProgressiveNameAlreadyUsed_ShouldContinueFromItsIndex()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate(
            "PostePay (2)",
            ["PostePay", "PostePay (2)", "PostePay (3)"]);

        suggestion.ShouldBe("PostePay (4)");
    }

    [Fact]
    public void DuplicateName_ShouldIgnoreCase()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate("postepay", ["PostePay"]);

        suggestion.ShouldBe("postepay (2)");
    }

    [Fact]
    public void BlankName_ShouldReturnNull()
    {
        var suggestion = NameUniqueness.SuggestIfDuplicate("   ", ["PostePay"]);

        suggestion.ShouldBeNull();
    }
}
