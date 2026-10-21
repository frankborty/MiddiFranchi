using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Costruisce il riepilogo investimenti alla data corrente. Tutti i valori sono in EUR.
/// La valorizzazione usa <see cref="PortfolioValuationCalculator"/>; il costo residuo usa
/// <see cref="InvestmentCostBasisCalculator"/>. Nessun valore mancante diventa zero.
/// </summary>
public sealed class InvestmentOverviewService
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly IInvestmentPortfolioRepository _portfolios;
    private readonly IFinancialOperationRepository _operations;
    private readonly IPriceObservationRepository _prices;
    private readonly Func<DateOnly> _currentUtcDate;

    public InvestmentOverviewService(
        IInvestmentPortfolioRepository portfolios,
        IFinancialOperationRepository operations,
        IPriceObservationRepository prices,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _portfolios = portfolios;
        _operations = operations;
        _prices = prices;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<InvestmentOverview> LoadAsync(CancellationToken cancellationToken = default)
    {
        var valuationDate = _currentUtcDate();
        var portfolios = await _portfolios.ListAsync(cancellationToken);
        var operations = await _operations.ListAsync(cancellationToken);
        var investmentOperations = operations.OfType<InvestmentOperation>().ToArray();
        var prices = await _prices.ListAsync(cancellationToken);

        var portfolioOverviews = new List<InvestmentPortfolioOverview>(portfolios.Count);
        foreach (var portfolio in portfolios)
        {
            var valuation = PortfolioValuationCalculator.Valuate(
                portfolio,
                investmentOperations,
                prices,
                valuationDate,
                ValuationPolicy.Unbounded);
            var costBases = InvestmentCostBasisCalculator.Calculate(
                portfolio,
                investmentOperations,
                valuationDate);

            portfolioOverviews.Add(BuildPortfolio(portfolio, valuation, costBases));
        }

        var history = BuildHistory(valuationDate, portfolios, investmentOperations, prices);

        return BuildOverview(valuationDate, portfolioOverviews, history);
    }

    private static InvestmentPortfolioOverview BuildPortfolio(
        InvestmentPortfolio portfolio,
        PortfolioValuation valuation,
        IReadOnlyList<InvestmentCostBasis> costBases)
    {
        var assets = new List<InvestmentAssetOverview>(valuation.Positions.Count);

        foreach (var position in valuation.Positions)
        {
            var instrument = position.Position.Instrument;
            var costBasis = costBases.FirstOrDefault(cost => cost.Instrument.Id == instrument.Id);
            var invested = costBasis?.CostBasisEur;
            var current = position.Value;
            Money? performance = invested is { } investedValue && current is { } currentValue
                ? currentValue - investedValue
                : null;
            var performancePercent = performance is { } performanceValue &&
                                     invested is { } investedBase &&
                                     investedBase.Amount > 0m
                ? performanceValue.Amount / investedBase.Amount
                : (decimal?)null;
            var reason = BuildAssetReason(position, costBasis);

            assets.Add(new InvestmentAssetOverview(
                instrument.Id,
                instrument.Name,
                instrument.Symbol,
                instrument.Type,
                position.Position.Quantity,
                invested,
                current,
                performance,
                performancePercent,
                reason is null,
                reason));
        }

        var complete = assets.Count > 0 && assets.All(asset => asset.IsComplete);
        var reasons = assets
            .Where(asset => !asset.IsComplete)
            .Select(asset => asset.IncompletenessReason!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Money? investedTotal = complete
            ? new Money(assets.Sum(asset => asset.InvestedCapital!.Value.Amount), portfolio.ReferenceCurrency)
            : null;
        Money? currentTotal = complete
            ? new Money(assets.Sum(asset => asset.CurrentValue!.Value.Amount), portfolio.ReferenceCurrency)
            : null;
        Money? performanceTotal = investedTotal is { } investedMoney && currentTotal is { } currentMoney
            ? currentMoney - investedMoney
            : null;
        var performancePercentTotal = performanceTotal is { } performanceMoney &&
                                      investedTotal is { } investedForPercent &&
                                      investedForPercent.Amount > 0m
            ? performanceMoney.Amount / investedForPercent.Amount
            : (decimal?)null;

        if (assets.Count == 0)
        {
            reasons = ["Nessuna posizione aperta alla data di valutazione."];
        }

        return new InvestmentPortfolioOverview(
            portfolio.Id,
            portfolio.Name,
            portfolio.ReferenceCurrency,
            investedTotal,
            currentTotal,
            performanceTotal,
            performancePercentTotal,
            complete,
            reasons,
            assets);
    }

    private static InvestmentOverview BuildOverview(
        DateOnly valuationDate,
        IReadOnlyList<InvestmentPortfolioOverview> portfolios,
        InvestmentHistory history)
    {
        var activePortfolios = portfolios
            .Where(portfolio => portfolio.Assets.Count > 0)
            .ToArray();
        var assetCount = activePortfolios.Sum(portfolio => portfolio.Assets.Count);

        if (assetCount == 0)
        {
            return new InvestmentOverview(
                valuationDate,
                null,
                null,
                null,
                null,
                false,
                ["Nessuna posizione aperta alla data di valutazione."],
                portfolios,
                history);
        }

        var complete = activePortfolios.All(portfolio => portfolio.IsComplete);
        if (!complete)
        {
            var reasons = activePortfolios
                .SelectMany(portfolio => portfolio.IncompletenessReasons)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new InvestmentOverview(
                valuationDate,
                null,
                null,
                null,
                null,
                false,
                reasons.Length > 0
                    ? reasons
                    : ["Alcune posizioni non hanno un valore o un costo residuo disponibile."],
                portfolios,
                history);
        }

        var invested = new Money(
            activePortfolios.Sum(portfolio => portfolio.InvestedCapital!.Value.Amount),
            Euro);
        var current = new Money(
            activePortfolios.Sum(portfolio => portfolio.CurrentValue!.Value.Amount),
            Euro);
        var performance = current - invested;
        var performancePercent = invested.Amount > 0m
            ? performance.Amount / invested.Amount
            : (decimal?)null;

        return new InvestmentOverview(
            valuationDate,
            invested,
            current,
            performance,
            performancePercent,
            true,
            [],
            portfolios,
            history);
    }

    private static InvestmentHistory BuildHistory(
        DateOnly valuationDate,
        IReadOnlyList<InvestmentPortfolio> portfolios,
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
            decimal? investedTotal = 0m;
            decimal? currentTotal = 0m;

            foreach (var portfolio in portfolios)
            {
                var valuation = PortfolioValuationCalculator.Valuate(
                    portfolio, operations, prices, date, ValuationPolicy.Unbounded);
                var costBases = InvestmentCostBasisCalculator.Calculate(
                    portfolio, operations, date);

                Money? current;
                if (valuation.Positions.Count == 0)
                {
                    current = new Money(0m, portfolio.ReferenceCurrency);
                }
                else if (valuation.IsComplete)
                {
                    current = new Money(
                        valuation.Positions.Sum(position => position.Value!.Value.Amount),
                        portfolio.ReferenceCurrency);
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
                        costsByInstrument.TryGetValue(position.Position.Instrument.Id, out var cost) &&
                        cost.CostBasisEur is not null);

                    if (allCostsAvailable)
                    {
                        invested = new Money(
                            valuation.Positions.Sum(position =>
                                costsByInstrument[position.Position.Instrument.Id].CostBasisEur!.Value.Amount),
                            Euro);
                    }
                    else
                    {
                        invested = null;
                        reasons.Add("Il costo residuo non è determinabile per una o più posizioni alla data storica.");
                    }
                }

                investedTotal = investedTotal is null || invested is null
                    ? null
                    : investedTotal + invested.Value.Amount;
                currentTotal = currentTotal is null || current is null
                    ? null
                    : currentTotal + current.Value.Amount;
            }

            points.Add(new InvestmentHistoryPoint(
                date,
                investedTotal is { } investedValue ? new Money(investedValue, Euro) : null,
                currentTotal is { } currentValue ? new Money(currentValue, Euro) : null));
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
