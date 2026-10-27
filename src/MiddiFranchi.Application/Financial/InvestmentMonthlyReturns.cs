using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Calcola il rendimento mensile con il metodo Modified Dietz: la variazione di valore viene
/// depurata dai flussi esterni (acquisti, vendite, cedole e dividendi) e include plusvalenze
/// realizzate, proventi e costi. In questo modo una vendita con guadagno non produce una perdita
/// artificiale nel mese in cui avviene.
/// </summary>
internal static class InvestmentMonthlyReturns
{
    private const string InvestmentFeesCategory = "Fees";
    private const string InvestmentTaxSubcategory = "Tasse Investimenti";

    public static IReadOnlyList<InvestmentMonthlyReturn> Calculate(
        IReadOnlyList<InvestmentHistoryPoint> history,
        IReadOnlyList<FinancialOperation> operations,
        DateOnly valuationDate)
    {
        var snapshots = BuildSnapshots(history, valuationDate);
        if (snapshots.Count < 2)
        {
            return [];
        }

        var flows = BuildFlowContext(operations);
        var returns = new List<InvestmentMonthlyReturn>(snapshots.Count - 1);

        for (var index = 1; index < snapshots.Count; index++)
        {
            var start = snapshots[index - 1];
            var end = snapshots[index];
            var period = CollectPeriodFlows(flows, start.Date, end.Date);
            returns.Add(new InvestmentMonthlyReturn(
                end.Month,
                CalculateReturn(start, end, period)));
        }

        return returns;
    }

    private static IReadOnlyList<Snapshot> BuildSnapshots(
        IReadOnlyList<InvestmentHistoryPoint> history,
        DateOnly valuationDate)
    {
        var points = history
            .Where(point => point.CurrentValue is not null && point.InvestedCapital is not null)
            .OrderBy(point => point.Date)
            .ToArray();
        if (points.Length == 0)
        {
            return [];
        }

        var firstMonth = new DateOnly(points[0].Date.Year, points[0].Date.Month, 1);
        var lastMonth = new DateOnly(valuationDate.Year, valuationDate.Month, 1);
        var snapshots = new List<Snapshot>();
        var pointIndex = 0;

        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            while (pointIndex + 1 < points.Length && points[pointIndex + 1].Date <= monthEnd)
            {
                pointIndex++;
            }

            if (points[pointIndex].Date > monthEnd)
            {
                continue;
            }

            snapshots.Add(new Snapshot(
                month,
                points[pointIndex].Date,
                points[pointIndex].CurrentValue!.Value.Amount));
        }

        return snapshots;
    }

    private static decimal? CalculateReturn(Snapshot start, Snapshot end, PeriodFlows flows)
    {
        var totalFlow = flows.Entries.Sum(entry => entry.Amount);
        var gain = end.Current - start.Current - totalFlow - flows.Costs;
        var weightedFlow = WeightedFlow(flows.Entries, start.Date, end.Date);
        var denominator = start.Current + weightedFlow;

        return denominator > 0m ? gain / denominator : null;
    }

    private static decimal WeightedFlow(
        IReadOnlyList<CashFlow> flows,
        DateOnly from,
        DateOnly to)
    {
        var periodDays = to.DayNumber - from.DayNumber;
        if (periodDays <= 0)
        {
            return 0m;
        }

        return flows.Sum(flow =>
        {
            var remainingDays = to.DayNumber - flow.Date.DayNumber;
            var weight = Math.Clamp(remainingDays / (decimal)periodDays, 0m, 1m);
            return flow.Amount * weight;
        });
    }

    private static FlowContext BuildFlowContext(IReadOnlyList<FinancialOperation> operations)
    {
        var taxExpenses = operations
            .OfType<Expense>()
            .Where(expense =>
                expense.Status == OperationStatus.Registered &&
                string.Equals(expense.Category.Name, InvestmentFeesCategory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(expense.Category.Subcategory, InvestmentTaxSubcategory, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(expense => expense.Id, expense => expense.Amount.Value.Amount);

        var linkedTaxExpenseIds = new HashSet<Guid>();
        foreach (var operation in operations)
        {
            if (operation.Status != OperationStatus.Registered)
            {
                continue;
            }

            if (operation is InvestmentOperation investment && investment.Instrument is not null)
            {
                MarkLinkedTax(investment.Id, taxExpenses, linkedTaxExpenseIds);
            }
            else if (operation is Income income && income.Instrument is not null)
            {
                MarkLinkedTax(income.Id, taxExpenses, linkedTaxExpenseIds);
            }
        }

        return new FlowContext(operations, taxExpenses, linkedTaxExpenseIds);
    }

    private static void MarkLinkedTax(
        Guid operationId,
        IReadOnlyDictionary<Guid, decimal> taxExpenses,
        ISet<Guid> linkedTaxExpenseIds)
    {
        var taxId = InvestmentExpenses.TaxIdForOperation(operationId);
        if (taxExpenses.ContainsKey(taxId))
        {
            linkedTaxExpenseIds.Add(taxId);
        }
    }

    private static PeriodFlows CollectPeriodFlows(
        FlowContext context,
        DateOnly fromExclusive,
        DateOnly toInclusive)
    {
        var entries = new List<CashFlow>();
        var costs = 0m;

        foreach (var operation in context.Operations)
        {
            if (operation.Status != OperationStatus.Registered ||
                operation.OperationDate <= fromExclusive ||
                operation.OperationDate > toInclusive)
            {
                continue;
            }

            switch (operation)
            {
                case InvestmentOperation investment:
                    AddInvestmentFlow(context, investment, entries, ref costs);
                    break;

                case Income income when income.Instrument is not null:
                    entries.Add(new CashFlow(
                        income.OperationDate,
                        -NetIncome(income.Amount.Value.Amount, income.Id, [], context)));
                    break;

                case Expense expense when
                    context.TaxExpenses.ContainsKey(expense.Id) &&
                    !context.LinkedTaxExpenseIds.Contains(expense.Id):
                    costs += expense.Amount.Value.Amount;
                    break;
            }
        }

        return new PeriodFlows(entries, costs);
    }

    private static void AddInvestmentFlow(
        FlowContext context,
        InvestmentOperation operation,
        ICollection<CashFlow> entries,
        ref decimal costs)
    {
        switch (operation.InvestmentKind)
        {
            case InvestmentOperationKind.Buy:
            {
                var gross = GrossAmount(operation);
                var fees = FeeAmount(operation, FeeKind.BuyFee);
                entries.Add(new CashFlow(operation.OperationDate, gross + fees));
                break;
            }

            case InvestmentOperationKind.Sell:
            {
                var gross = GrossAmount(operation);
                var fees = FeeAmount(operation, FeeKind.SellFee);
                var tax = TaxAmount(operation.Id, operation.Fees, context);
                entries.Add(new CashFlow(operation.OperationDate, -(gross - fees - tax)));
                break;
            }

            case InvestmentOperationKind.Dividend:
            case InvestmentOperationKind.Coupon:
            {
                var gross = operation.Amount?.Value.Amount ?? 0m;
                entries.Add(new CashFlow(
                    operation.OperationDate,
                    -NetIncome(gross, operation.Id, operation.Fees, context)));
                break;
            }

            case InvestmentOperationKind.StandaloneFee:
                costs += FeeAmount(operation, FeeKind.Standalone);
                break;
        }
    }

    private static decimal GrossAmount(InvestmentOperation operation) =>
        (operation.UnitPrice?.Price.Amount ?? 0m) * (operation.Quantity?.Value ?? 0m);

    private static decimal FeeAmount(InvestmentOperation operation, FeeKind kind) =>
        operation.Fees
            .Where(fee => fee.Kind == kind)
            .Sum(fee => fee.Amount.Value.Amount);

    private static decimal NetIncome(
        decimal gross,
        Guid operationId,
        IReadOnlyList<FeeComponent> fees,
        FlowContext context) =>
        gross - TaxAmount(operationId, fees, context);

    private static decimal TaxAmount(
        Guid operationId,
        IReadOnlyList<FeeComponent> fees,
        FlowContext context)
    {
        var taxId = InvestmentExpenses.TaxIdForOperation(operationId);
        if (context.TaxExpenses.TryGetValue(taxId, out var linkedTax))
        {
            return linkedTax;
        }

        return fees
            .Where(fee => fee.Kind == FeeKind.TaxWithheld)
            .Sum(fee => fee.Amount.Value.Amount);
    }

    private sealed record Snapshot(DateOnly Month, DateOnly Date, decimal Current);

    private sealed record CashFlow(DateOnly Date, decimal Amount);

    private sealed record PeriodFlows(IReadOnlyList<CashFlow> Entries, decimal Costs);

    private sealed record FlowContext(
        IReadOnlyList<FinancialOperation> Operations,
        IReadOnlyDictionary<Guid, decimal> TaxExpenses,
        IReadOnlySet<Guid> LinkedTaxExpenseIds);
}
