using MiddiFranchi.Application.DataImport;
using MiddiFranchi.Application.Shared;
using MiddiFranchi.Domain.Financial;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class ImportIdMapperTests
{
    [Fact]
    public void ToGuid_ShouldBeDeterministic()
    {
        var first = ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, "conto-1");
        var second = ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, "conto-1");

        first.ShouldBe(second);
        first.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ToGuid_ShouldIncludeEntityAndKey()
    {
        var account = ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, "1");
        var instrument = ImportIdMapper.ToGuid(ImportIdMapper.InstrumentEntity, "1");
        var otherKey = ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, "2");

        account.ShouldNotBe(instrument);
        account.ShouldNotBe(otherKey);
    }

    [Fact]
    public void ToGuid_ShouldTrimKey()
    {
        var trimmed = ImportIdMapper.ToGuid(ImportIdMapper.OperationEntity, "op-1");
        var padded = ImportIdMapper.ToGuid(ImportIdMapper.OperationEntity, " op-1 ");

        trimmed.ShouldBe(padded);
    }

    [Fact]
    public void ToInstrumentGuidByIsin_ShouldBeCaseInsensitiveAndTrimmed()
    {
        var first = ImportIdMapper.ToInstrumentGuidByIsin("it0000000001");
        var same = ImportIdMapper.ToInstrumentGuidByIsin(" IT0000000001 ");

        first.ShouldBe(same);
        first.ShouldNotBe(ImportIdMapper.ToGuid(
            ImportIdMapper.InstrumentEntity,
            "IT0000000001"));
    }

    [Fact]
    public void ToAccountBalanceGuid_ShouldDependOnTheAccountNameAndDate()
    {
        var first = ImportIdMapper.ToAccountBalanceGuid(
            "Conto corrente",
            new DateOnly(2024, 1, 1));
        var same = ImportIdMapper.ToAccountBalanceGuid(
            "  conto CORRENTE  ",
            new DateOnly(2024, 1, 1));
        var otherDate = ImportIdMapper.ToAccountBalanceGuid(
            "Conto corrente",
            new DateOnly(2024, 1, 2));

        first.ShouldBe(same);
        first.ShouldNotBe(otherDate);
    }

    [Fact]
    public void ToInstrumentPriceGuid_ShouldDependOnTheInstrumentAndDate()
    {
        var instrumentId = Guid.NewGuid();
        var first = ImportIdMapper.ToInstrumentPriceGuid(
            instrumentId,
            new DateOnly(2024, 1, 1));
        var same = ImportIdMapper.ToInstrumentPriceGuid(
            instrumentId,
            new DateOnly(2024, 1, 1));
        var otherDate = ImportIdMapper.ToInstrumentPriceGuid(
            instrumentId,
            new DateOnly(2024, 1, 2));
        var otherInstrument = ImportIdMapper.ToInstrumentPriceGuid(
            Guid.NewGuid(),
            new DateOnly(2024, 1, 1));

        first.ShouldBe(same);
        first.ShouldNotBe(otherDate);
        first.ShouldNotBe(otherInstrument);
    }

    [Fact]
    public void DeterministicId_ShouldProduceTheTaxExpenseIdUsedForExistingData()
    {
        var operationId = Guid.Parse("af2d8016-181b-f659-b9c4-889ed6badc10");

        DeterministicId.From("tax-expense", operationId.ToString("N"))
            .ShouldBe(Guid.Parse("0298b52d-f3a0-1755-bbc7-d9df94a01b42"));
    }

    [Fact]
    public void ToOperationGuid_ShouldDependOnTheNaturalKey()
    {
        var instrumentId = Guid.NewGuid();
        var first = ImportIdMapper.ToOperationGuid(
            instrumentId,
            InvestmentOperationKind.Buy,
            new DateOnly(2024, 1, 1),
            10m,
            100.50m,
            2.50m);
        var same = ImportIdMapper.ToOperationGuid(
            instrumentId,
            InvestmentOperationKind.Buy,
            new DateOnly(2024, 1, 1),
            10.000m,
            100.50m,
            2.50m);

        first.ShouldBe(same);
        first.ShouldNotBe(ImportIdMapper.ToOperationGuid(
            instrumentId,
            InvestmentOperationKind.Sell,
            new DateOnly(2024, 1, 1),
            10m,
            100.50m,
            2.50m));
        first.ShouldNotBe(ImportIdMapper.ToOperationGuid(
            instrumentId,
            InvestmentOperationKind.Buy,
            new DateOnly(2024, 1, 2),
            10m,
            100.50m,
            2.50m));
    }
}
