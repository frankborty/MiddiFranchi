using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class NetWorthSummaryTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly DateOnly ValuationDate = new(2026, 3, 31);

    [Fact]
    public void Construction_ShouldRejectNullCollections()
    {
        Should.Throw<ArgumentNullException>(
                () => new NetWorthSummary(ValuationDate, Euro, null!, [], null, null, null, []))
            .ParamName.ShouldBe("accounts");

        Should.Throw<ArgumentNullException>(
                () => new NetWorthSummary(ValuationDate, Euro, [], null!, null, null, null, []))
            .ParamName.ShouldBe("investments");

        Should.Throw<ArgumentNullException>(
                () => new NetWorthSummary(ValuationDate, Euro, [], [], null, null, null, null!))
            .ParamName.ShouldBe("incompletenessReasons");
    }

    [Fact]
    public void Construction_ShouldRejectADefaultReportingCurrency()
    {
        Should.Throw<ArgumentException>(
                () => new NetWorthSummary(ValuationDate, default, [], [], null, null, null, []))
            .ParamName.ShouldBe("reportingCurrency");
    }
}
