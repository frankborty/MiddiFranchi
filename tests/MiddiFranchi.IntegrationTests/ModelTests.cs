using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Entities;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

public class ModelTests
{
    [Fact]
    public void Model_ShouldExposeTheExpectedTables()
    {
        using var context = CreateContext();
        var model = GetModel(context);

        var tables = model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Distinct()
            .ToArray();

        tables.ShouldContain("Accounts");
        tables.ShouldContain("Categories");
        tables.ShouldContain("InvestmentPortfolios");
        tables.ShouldContain("Instruments");
        tables.ShouldContain("FinancialOperations");
        tables.ShouldContain("AccountPostings");
        tables.ShouldContain("InvestmentOperationFees");
        tables.ShouldContain("PriceObservations");
        tables.ShouldContain("ExchangeRateObservations");
    }

    [Fact]
    public void Model_ShouldUseExplicitPrecisionAndTemporalTypes()
    {
        using var context = CreateContext();
        var model = GetModel(context);
        var operation = model.FindEntityType(typeof(FinancialOperationRecord)).ShouldNotBeNull();

        operation.FindProperty(nameof(FinancialOperationRecord.AmountAmount))!
            .GetColumnType().ShouldBe("numeric");
        operation.FindProperty(nameof(FinancialOperationRecord.OperationDate))!
            .GetColumnType().ShouldBe("date");
        operation.FindProperty(nameof(FinancialOperationRecord.RecordedAt))!
            .GetColumnType().ShouldBe("timestamptz");

        var posting = model.FindEntityType(typeof(AccountPostingRecord)).ShouldNotBeNull();
        posting.FindProperty(nameof(AccountPostingRecord.CurrencyCode))!
            .GetMaxLength().ShouldBe(3);
        posting.FindProperty(nameof(AccountPostingRecord.SignedAmount))!
            .GetColumnType().ShouldBe("numeric");
    }

    [Fact]
    public void Model_ShouldMapDomainEnumsAsStrings()
    {
        using var context = CreateContext();
        var model = GetModel(context);
        var operation = model.FindEntityType(typeof(FinancialOperationRecord)).ShouldNotBeNull();

        operation.FindProperty(nameof(FinancialOperationRecord.Kind))!
            .GetProviderClrType().ShouldBe(typeof(string));
        operation.FindProperty(nameof(FinancialOperationRecord.InvestmentKind))!
            .GetProviderClrType().ShouldBe(typeof(string));
        operation.FindProperty("Status").ShouldBeNull();
    }

    [Fact]
    public void Model_ShouldProtectAccountingRelationsWithRestrict()
    {
        using var context = CreateContext();
        var model = GetModel(context);
        var posting = model.FindEntityType(typeof(AccountPostingRecord)).ShouldNotBeNull();

        posting.GetForeignKeys().ShouldNotBeEmpty();
        posting.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void Model_ShouldExposeUniquenessAndCheckConstraints()
    {
        using var context = CreateContext();
        var model = GetModel(context);
        var operation = model.FindEntityType(typeof(FinancialOperationRecord)).ShouldNotBeNull();
        var fee = model.FindEntityType(typeof(FeeComponentRecord)).ShouldNotBeNull();

        operation.GetIndexes().ShouldContain(index =>
            index.IsUnique &&
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(FinancialOperationRecord.OriginalOperationId));

        operation.GetCheckConstraints().ShouldContain(constraint =>
            constraint.Name == "CK_FinancialOperations_QuotedPriceShape");
        operation.GetCheckConstraints().ShouldContain(constraint =>
            constraint.Name == "CK_FinancialOperations_ReversalLink");
        operation.GetCheckConstraints().ShouldContain(constraint =>
            constraint.Name == "CK_FinancialOperations_InvestmentDetail");
        operation.GetCheckConstraints().ShouldContain(constraint =>
            constraint.Name == "CK_FinancialOperations_NonInvestmentFields");
        operation.FindProperty("Status").ShouldBeNull();

        fee.GetIndexes().ShouldContain(index =>
            index.IsUnique &&
            index.Properties.Any(property => property.Name == nameof(FeeComponentRecord.InvestmentOperationId)) &&
            index.Properties.Any(property => property.Name == nameof(FeeComponentRecord.Ordinal)));
    }

    private static IModel GetModel(MiddiFranchiDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static MiddiFranchiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MiddiFranchiDbContext>()
            .UseNpgsql("Host=localhost;Database=middifranchi_model_tests")
            .Options;

        return new MiddiFranchiDbContext(options);
    }
}
