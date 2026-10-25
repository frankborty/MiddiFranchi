using MiddiFranchi.Application.DataManagement;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class DataDeletionPlannerTests
{
    private static readonly DataCounts Counts = new(
        Accounts: 4,
        Categories: 10,
        Instruments: 3,
        FinancialOperations: 25,
        AccountPostings: 12,
        FeeComponents: 2,
        PriceObservations: 40,
        ManualBalances: 8,
        Loans: 1,
        LoanReadings: 2,
        AppMetadata: 3);

    [Fact]
    public void Build_Movements_ShouldNotRequireOtherDatasets()
    {
        var plan = DataDeletionPlanner.Build(DataDataset.Movements, Counts);

        plan.Effective.ShouldBe(DataDataset.Movements);
        plan.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DataDataset.Accounts, "conti")]
    [InlineData(DataDataset.Instruments, "strumenti")]
    [InlineData(DataDataset.Categories, "categorie")]
    public void Build_DatasetWithDependencies_ShouldIncludeMovements(DataDataset dataset, string label)
    {
        var plan = DataDeletionPlanner.Build(dataset, Counts);

        plan.Effective.ShouldBe(dataset | DataDataset.Movements);
        plan.Warnings.Count.ShouldBe(1);
        plan.Warnings[0].ShouldContain(label);
        plan.Warnings[0].ShouldContain("25 operazioni");
    }

    [Fact]
    public void Build_Everything_ShouldNotAddWarnings()
    {
        var plan = DataDeletionPlanner.Build(DataDataset.Everything, Counts);

        plan.Effective.ShouldBe(DataDataset.Everything);
        plan.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Build_None_ShouldReturnEmptyPlan()
    {
        var plan = DataDeletionPlanner.Build(DataDataset.None, Counts);

        plan.IsEmpty.ShouldBeTrue();
        plan.Effective.ShouldBe(DataDataset.None);
    }

    [Fact]
    public void Build_SingleMovementWarning_ShouldUseSingular()
    {
        var counts = Counts with { FinancialOperations = 1 };

        var plan = DataDeletionPlanner.Build(DataDataset.Accounts, counts);

        plan.Warnings[0].ShouldContain("1 operazione");
    }

    [Fact]
    public void DatasetsOf_ShouldReturnSingleFlagsInOrder()
    {
        var datasets = DataDeletionPlanner.DatasetsOf(
            DataDataset.Categories | DataDataset.Movements | DataDataset.Accounts);

        datasets.ShouldBe([DataDataset.Movements, DataDataset.Accounts, DataDataset.Categories]);
    }
}
