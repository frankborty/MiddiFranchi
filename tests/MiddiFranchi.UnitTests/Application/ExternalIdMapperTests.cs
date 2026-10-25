using MiddiFranchi.Application.DataImport;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class ExternalIdMapperTests
{
    [Fact]
    public void ToGuid_ShouldBeDeterministic()
    {
        var first = ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, "conto-1");
        var second = ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, "conto-1");

        first.ShouldBe(second);
        first.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void ToGuid_ShouldIncludeEntityAndExternalId()
    {
        var account = ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, "1");
        var instrument = ExternalIdMapper.ToGuid(ExternalIdMapper.InstrumentEntity, "1");
        var otherExternalId = ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, "2");

        account.ShouldNotBe(instrument);
        account.ShouldNotBe(otherExternalId);
    }

    [Fact]
    public void ToGuid_ShouldTrimExternalId()
    {
        var trimmed = ExternalIdMapper.ToGuid(ExternalIdMapper.OperationEntity, "op-1");
        var padded = ExternalIdMapper.ToGuid(ExternalIdMapper.OperationEntity, " op-1 ");

        trimmed.ShouldBe(padded);
    }
}
