using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Costruisce il riepilogo investimenti alla data corrente. Tutti i valori sono in EUR.
/// La valorizzazione usa <see cref="InvestmentValuationCalculator"/>; il costo residuo usa
/// <see cref="InvestmentCostBasisCalculator"/> e il risultato totale usa
/// <see cref="InvestmentResultCalculator"/>. Nessun valore mancante diventa zero.
/// </summary>
public sealed class InvestmentOverviewService
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly IFinancialOperationRepository _operations;
    private readonly IPriceObservationRepository _prices;
    private readonly Func<DateOnly> _currentUtcDate;

    public InvestmentOverviewService(
        IFinancialOperationRepository operations,
        IPriceObservationRepository prices,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _operations = operations;
        _prices = prices;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<InvestmentOverview> LoadAsync(CancellationToken cancellationToken = default)
    {
        var valuationDate = _currentUtcDate();
        var allOperations = await _operations.ListAsync(cancellationToken);
        var investmentOperations = allOperations
            .OfType<InvestmentOperation>()
            .ToArray();
        var ordinaryIncomeByInstrument = allOperations
            .OfType<Income>()
            .Where(income =>
                income.Status == OperationStatus.Registered &&
                income.Instrument is not null)
            .GroupBy(income => income.Instrument!.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(income => income.Amount.Value.Amount));
        var ordinaryIncome = new Money(ordinaryIncomeByInstrument.Values.Sum(), Euro);
        var prices = await _prices.ListAsync(cancellationToken);

        var valuation = InvestmentValuationCalculator.Valuate(
            investmentOperations,
            prices,
            valuationDate,
            ValuationPolicy.Unbounded);
        var costBases = InvestmentCostBasisCalculator.Calculate(investmentOperations, valuationDate);
        var results = InvestmentResultCalculator.Calculate(
            investmentOperations,
            prices,
            valuationDate,
            ValuationPolicy.Unbounded);
        var resultsByInstrument = results.Assets.ToDictionary(result => result.Instrument.Id);

        var assets = new List<InvestmentAssetOverview>(valuation.Positions.Count);
        foreach (var position in valuation.Positions)
        {
            var instrument = position.Position.Instrument;
            var costBasis = costBases.FirstOrDefault(cost => cost.Instrument.Id == instrument.Id);
            var result = resultsByInstrument.GetValueOrDefault(instrument.Id);
            var invested = costBasis?.CostBasis;
            var current = position.Value;
            Money? unrealized = invested is { } investedValue && current is { } currentValue
                ? currentValue - investedValue
                : null;
            var total = result?.TotalResult;
            var totalPercent = total is { } totalValue && invested is { } investedBase && investedBase.Amount > 0m
                ? totalValue.Amount / investedBase.Amount
                : (decimal?)null;
            var reason = BuildAssetReason(position, costBasis);

            assets.Add(new InvestmentAssetOverview(
                instrument.Id,
                instrument.Name,
                instrument.Symbol,
                instrument.AssetClass,
                position.Position.Quantity,
                invested,
                costBasis?.AverageCost,
                current,
                unrealized,
                result?.RealizedResult ?? new Money(0m, instrument.QuoteCurrency),
                (result?.Income ?? new Money(0m, instrument.QuoteCurrency)) +
                new Money(ordinaryIncomeByInstrument.GetValueOrDefault(instrument.Id), Euro),
                result?.OtherFees ?? new Money(0m, instrument.QuoteCurrency),
                total,
                totalPercent,
                reason is null,
                reason));
        }

        var history = BuildHistory(valuationDate, investmentOperations, prices);

        return BuildOverview(valuationDate, assets, results, ordinaryIncome, history);
    }

    private static InvestmentOverview BuildOverview(
        DateOnly valuationDate,
        IReadOnlyList<InvestmentAssetOverview> assets,
        InvestmentResultSet results,
        Money ordinaryIncome,
        InvestmentHistory history)
    {
        var complete = results.IsComplete && assets.All(asset => asset.IsComplete);
        var reasons = assets
            .Where(asset => !asset.IsComplete)
            .Select(asset => asset.IncompletenessReason!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Money? invested = complete
            ? new Money(assets.Sum(asset => asset.InvestedCapital!.Value.Amount), Euro)
            : null;
        Money? current = complete
            ? new Money(assets.Sum(asset => asset.CurrentValue!.Value.Amount), Euro)
            : null;
        Money? performance = invested is { } investedMoney && current is { } currentMoney
            ? currentMoney - investedMoney
            : null;
        var performancePercent = performance is { } performanceMoney &&
                                 invested is { } investedForPercent &&
                                 investedForPercent.Amount > 0m
            ? performanceMoney.Amount / investedForPercent.Amount
            : (decimal?)null;
        var total = complete ? results.TotalResult : null;
        var totalPercent = total is { } totalMoney &&
                           invested is { } totalBase &&
                           totalBase.Amount > 0m
            ? totalMoney.Amount / totalBase.Amount
            : (decimal?)null;

        return new InvestmentOverview(
            valuationDate,
            invested,
            current,
            performance,
            performancePercent,
            results.RealizedResult,
            results.Income + ordinaryIncome,
            results.OtherFees,
            total,
            totalPercent,
            complete,
            complete ? [] : reasons,
            assets,
            history);
    }

    private static InvestmentHistory BuildHistory(
        DateOnly valuationDate,
        IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyList<PriceObservation> prices)
    {
        var positionOperations = operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.InvestmentKind is InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .Where(operation => operation.OperationDate <= valuationDate)
            .ToArray();

        if (positionOperations.Length == 0)
        {
            return new InvestmentHistory(valuationDate, valuationDate, Euro, [], true, []);
        }

        var from = positionOperations.Min(operation => operation.OperationDate);
        var relevantInstrumentIds = positionOperations
            .Where(operation => operation.Instrument is not null)
            .Select(operation => operation.Instrument!.Id)
            .ToHashSet();
        var historyPrices = InvestmentHistoryPrices.WithExecutionPrices(prices, positionOperations);

        var operationDates = positionOperations
            .Select(operation => operation.OperationDate)
            .Where(date => date >= from && date <= valuationDate)
            .ToHashSet();

        var observationDates = prices
            .Where(price => relevantInstrumentIds.Contains(price.Instrument.Id))
            .Select(price => price.ValidAt)
            .Where(date => date >= from && date <= valuationDate)
            .ToHashSet();

        var dates = operationDates
            .Concat(observationDates)
            .Append(valuationDate)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        if (dates.Length > 60)
        {
            var stride = (int)Math.Ceiling(dates.Length / 60d);
            dates = dates
                .Where((date, index) => index % stride == 0)
                .Concat(operationDates)
                .Append(valuationDate)
                .Distinct()
                .OrderBy(date => date)
                .ToArray();
        }

        var points = new List<InvestmentHistoryPoint>(dates.Length);
        var reasons = new HashSet<string>(StringComparer.Ordinal);

        foreach (var date in dates)
        {
            var valuation = InvestmentValuationCalculator.Valuate(
                operations,
                historyPrices,
                date,
                ValuationPolicy.Unbounded);
            var costBases = InvestmentCostBasisCalculator.Calculate(operations, date);

            Money? current;
            if (valuation.Positions.Count == 0)
            {
                current = new Money(0m, Euro);
            }
            else if (valuation.IsComplete)
            {
                current = new Money(
                    valuation.Positions.Sum(position => position.Value!.Value.Amount),
                    Euro);
            }
            else
            {
                current = null;
                reasons.Add("Una o più posizioni non sono valorizzate alla data storica: mancano quotazioni.");
            }

            Money? invested;
            if (valuation.Positions.Count == 0)
            {
                invested = new Money(0m, Euro);
            }
            else
            {
                var costsByInstrument = costBases.ToDictionary(cost => cost.Instrument.Id);
                var allCostsAvailable = valuation.Positions.All(position =>
                    costsByInstrument.ContainsKey(position.Position.Instrument.Id));

                invested = allCostsAvailable
                    ? new Money(
                        valuation.Positions.Sum(position =>
                            costsByInstrument[position.Position.Instrument.Id].CostBasis.Amount),
                        Euro)
                    : null;

                if (!allCostsAvailable)
                {
                    reasons.Add("Il costo residuo non è determinabile per una o più posizioni alla data storica.");
                }
            }

            points.Add(new InvestmentHistoryPoint(date, invested, current));
        }

        var complete = points.All(point =>
            point.InvestedCapital is not null && point.CurrentValue is not null);

        return new InvestmentHistory(
            from,
            valuationDate,
            Euro,
            points,
            complete,
            reasons.ToArray());
    }

    private static string? BuildAssetReason(
        InvestmentPositionValuation position,
        InvestmentCostBasis? costBasis)
    {
        var reasons = new List<string>(2);

        if (position.Value is null)
        {
            reasons.Add(position.IncompletenessReason
                ?? "La posizione non è valorizzata alla data richiesta.");
        }

        if (costBasis is null)
        {
            reasons.Add("Il costo residuo della posizione non è disponibile.");
        }

        return reasons.Count == 0 ? null : string.Join(" ", reasons);
    }
}
