using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Costruisce il modello di lettura della pagina Investimenti. Il catalogo include tutti gli
/// strumenti, anche quelli senza posizione e quelli archiviati; nessun valore mancante diventa zero.
/// </summary>
public sealed class InvestmentWorkspaceService
{
    private const int StalePriceDays = 30;
    private static readonly Currency Euro = Currency.Euro;

    private readonly InvestmentOverviewService _overview;
    private readonly IInstrumentRepository _instruments;
    private readonly IPriceObservationRepository _prices;
    private readonly IFinancialOperationRepository _operations;
    private readonly IAccountRepository _accounts;
    private readonly Func<DateOnly> _currentUtcDate;

    public InvestmentWorkspaceService(
        InvestmentOverviewService overview,
        IInstrumentRepository instruments,
        IPriceObservationRepository prices,
        IFinancialOperationRepository operations,
        IAccountRepository accounts,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _overview = overview;
        _instruments = instruments;
        _prices = prices;
        _operations = operations;
        _accounts = accounts;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<InvestmentWorkspace> LoadAsync(CancellationToken cancellationToken = default)
    {
        var valuationDate = _currentUtcDate();
        var overview = await _overview.LoadAsync(cancellationToken);
        var instruments = await _instruments.ListAsync(cancellationToken);
        var prices = await _prices.ListAsync(cancellationToken);
        var allOperations = await _operations.ListAsync(cancellationToken);
        var operations = allOperations
            .OfType<InvestmentOperation>()
            .ToArray();
        var taxExpenseAmountsById = allOperations
            .OfType<Expense>()
            .Where(expense =>
                expense.Status == OperationStatus.Registered &&
                string.Equals(expense.Category.Name, "Fees", StringComparison.CurrentCultureIgnoreCase) &&
                string.Equals(expense.Category.Subcategory, "Tasse Investimenti", StringComparison.CurrentCultureIgnoreCase))
            .ToDictionary(
                expense => expense.Id,
                expense => expense.Amount.Value.Amount);
        var ordinaryIncomeByInstrument = allOperations
            .OfType<Income>()
            .Where(income =>
                income.Status == OperationStatus.Registered &&
                income.Instrument is not null)
            .GroupBy(income => income.Instrument!.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(income => income.Amount.Value.Amount));
        var accounts = await _accounts.ListAsync(cancellationToken);

        var accountsById = accounts.ToDictionary(account => account.Id);
        var results = InvestmentResultCalculator.Calculate(
            operations,
            prices,
            valuationDate,
            ValuationPolicy.Unbounded);
        var resultsByInstrument = results.Assets.ToDictionary(result => result.Instrument.Id);
        var operationsByInstrument = operations
            .Where(operation => operation.Instrument is not null)
            .GroupBy(operation => operation.Instrument!.Id)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(operation => operation.OperationDate)
                    .ThenByDescending(operation => operation.RecordedAt)
                    .ThenByDescending(operation => operation.Id)
                    .ToArray());

        var rows = instruments
            .Select(instrument => BuildRow(
                instrument,
                resultsByInstrument.GetValueOrDefault(instrument.Id),
                operationsByInstrument.GetValueOrDefault(instrument.Id) ?? [],
                prices,
                accountsById,
                taxExpenseAmountsById,
                ordinaryIncomeByInstrument.GetValueOrDefault(instrument.Id),
                valuationDate))
            .OrderBy(row => row.IsArchived)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.InstrumentId)
            .ToArray();

        var allocation = BuildAllocation(rows);
        var totalIncome = results.Income + new Money(
            ordinaryIncomeByInstrument.Values.Sum(),
            Euro);
        var incomeTaxes =
            allOperations
                .OfType<Income>()
                .Where(income => income.Status == OperationStatus.Registered)
                .Sum(income => taxExpenseAmountsById.GetValueOrDefault(
                    InvestmentExpenses.TaxIdForOperation(income.Id))) +
            operations
                .Where(operation =>
                    operation.Status == OperationStatus.Registered &&
                    operation.InvestmentKind is
                        InvestmentOperationKind.Coupon or
                        InvestmentOperationKind.Dividend)
                .Sum(operation => taxExpenseAmountsById.GetValueOrDefault(
                    InvestmentExpenses.TaxIdForOperation(operation.Id)));
        var netIncome = totalIncome - new Money(incomeTaxes, Euro);
        var monthlyReturns = InvestmentMonthlyReturns.Calculate(
            overview.History.Points,
            allOperations,
            valuationDate);
        return new InvestmentWorkspace(
            overview,
            rows,
            allocation,
            totalIncome,
            netIncome,
            rows.Count(row => row.IsArchived),
            rows.Length,
            monthlyReturns);
    }

    private static InvestmentAssetRow BuildRow(
        Instrument instrument,
        InvestmentResult? result,
        IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyList<PriceObservation> prices,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, decimal> taxExpenseAmountsById,
        decimal ordinaryIncome,
        DateOnly valuationDate)
    {
        var quantity = result?.Quantity;
        Money? invested = quantity is not null ? result!.CostBasis : null;
        Money? current = quantity is not null ? result!.CurrentValue : null;
        Money? unrealized = quantity is not null ? result!.UnrealizedResult : null;
        Money? averageCost = invested is { } investedMoney && quantity is { } held
            ? new Money(investedMoney.Amount / held.Value, Euro)
            : null;
        var averageCostPercent = averageCost is { } average &&
                                 instrument.PriceUnit == PriceUnit.PercentOfNominal &&
                                 instrument.NotionalValue is { Amount: > 0m } notional
            ? average.Amount / notional.Amount * 100m
            : (decimal?)null;
        var total = result?.TotalResult;
        var totalPercent = total is { } totalMoney &&
                           invested is { } investedBase &&
                           investedBase.Amount > 0m
            ? totalMoney.Amount / investedBase.Amount
            : (decimal?)null;

        var observation = PriceObservations.MostRecentOnOrBefore(prices, instrument, valuationDate);
        var currentUnitPrice = observation is null ? null : EffectiveUnitPrice(instrument, observation);
        var isStale = observation is not null &&
                      valuationDate.DayNumber - observation.ValidAt.DayNumber > StalePriceDays;

        var warnings = new List<string>();
        if (quantity is not null && current is null)
        {
            warnings.Add(result?.IncompletenessReason
                ?? "Nessuna quotazione disponibile: il valore attuale non è calcolabile.");
        }

        if (isStale && observation is not null)
        {
            warnings.Add(
                $"Ultima quotazione del {observation.ValidAt:yyyy-MM-dd}: potrebbe non riflettere il valore corrente.");
        }

        if (instrument.IsArchived && quantity is not null)
        {
            warnings.Add("Strumento archiviato con quote ancora aperte.");
        }

        var operationRows = operations
            .Select(operation => BuildOperationRow(operation, accounts, taxExpenseAmountsById))
            .ToArray();
        var history = BuildInstrumentHistory(
            instrument,
            operations,
            prices,
            valuationDate);
        var priceHistory = BuildInstrumentPriceHistory(
            instrument,
            operations,
            prices,
            valuationDate);

        return new InvestmentAssetRow(
            instrument.Id,
            instrument.Name,
            instrument.AssetClass,
            instrument.Status,
            instrument.Symbol,
            instrument.Isin,
            instrument.ReferenceUrl,
            instrument.Notes,
            instrument.QuoteCurrency,
            instrument.PriceUnit,
            instrument.NotionalValue,
            instrument.MaturityDate,
            instrument.CouponRate,
            instrument.TaxRate,
            quantity,
            averageCost,
            averageCostPercent,
            invested,
            current,
            unrealized,
            result?.RealizedResult ?? new Money(0m, instrument.QuoteCurrency),
            (result?.Income ?? new Money(0m, instrument.QuoteCurrency)) +
            new Money(ordinaryIncome, Euro),
            result?.OtherFees ?? new Money(0m, instrument.QuoteCurrency),
            total,
            totalPercent,
            currentUnitPrice,
            observation?.Price,
            observation?.ValidAt,
            observation?.TradeTime,
            observation?.ObservedAt,
            observation?.Source,
            isStale,
            operations.Count > 0,
            operationRows,
            warnings,
            history,
            priceHistory);
    }

    private static IReadOnlyList<InvestmentPricePoint> BuildInstrumentPriceHistory(
        Instrument instrument,
        IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyList<PriceObservation> prices,
        DateOnly valuationDate)
    {
        if (instrument.PriceUnit != PriceUnit.PerUnit)
        {
            return [];
        }

        var positionOperations = operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.InvestmentKind is
                InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .Where(operation => operation.OperationDate <= valuationDate)
            .ToArray();
        var instrumentPrices = prices
            .Where(price => price.Instrument.Id == instrument.Id)
            .ToArray();

        return InvestmentHistoryPrices
            .WithExecutionPrices(instrumentPrices, positionOperations)
            .Where(price => price.Instrument.Id == instrument.Id && price.ValidAt <= valuationDate)
            .OrderBy(price => price.ValidAt)
            .ThenBy(price => price.ObservedAt)
            .GroupBy(price => price.ValidAt)
            .Select(group => group.Last())
            .Select(price => new InvestmentPricePoint(
                price.ValidAt,
                price.Price,
                price.PriceCurrency ?? instrument.QuoteCurrency))
            .ToArray();
    }

    private static IReadOnlyList<InvestmentHistoryPoint> BuildInstrumentHistory(
        Instrument instrument,
        IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyList<PriceObservation> prices,
        DateOnly valuationDate)
    {
        var positionOperations = operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.InvestmentKind is
                InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .Where(operation => operation.OperationDate <= valuationDate)
            .ToArray();
        if (positionOperations.Length == 0)
        {
            return [];
        }

        var instrumentPrices = prices
            .Where(price => price.Instrument.Id == instrument.Id)
            .ToArray();
        var historyPrices = InvestmentHistoryPrices.WithExecutionPrices(instrumentPrices, positionOperations);
        var operationDates = positionOperations
            .Select(operation => operation.OperationDate)
            .ToArray();
        var observationDates = instrumentPrices
            .Select(price => price.ValidAt)
            .ToArray();

        var dates = InvestmentHistoryDates.Build(
            operationDates,
            observationDates,
            valuationDate);

        var points = new List<InvestmentHistoryPoint>(dates.Count);
        foreach (var date in dates)
        {
            var valuation = InvestmentValuationCalculator.Valuate(
                operations,
                historyPrices,
                date,
                ValuationPolicy.Unbounded);
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
            }

            var costBasis = InvestmentCostBasisCalculator
                .Calculate(operations, date)
                .FirstOrDefault(cost => cost.Instrument.Id == instrument.Id);
            Money? invested = costBasis?.CostBasis
                ?? (valuation.Positions.Count == 0 ? new Money(0m, Euro) : null);

            points.Add(new InvestmentHistoryPoint(date, invested, current));
        }

        return points;
    }

    private static InvestmentOperationRow BuildOperationRow(
        InvestmentOperation operation,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, decimal> taxExpenseAmountsById)
    {
        var quoted = operation.QuotedPrice;
        var effectiveUnitPrice = operation.UnitPrice?.Price;
        Money? grossAmount = effectiveUnitPrice is { } price && operation.Quantity is { } quantity
            ? new Money(price.Amount * quantity.Value, price.Currency)
            : null;
        var commission = operation.Fees
            .Where(fee => fee.Kind != FeeKind.TaxWithheld)
            .Sum(fee => fee.Amount.Value.Amount);
        var taxExpenseId = InvestmentExpenses.TaxIdForOperation(operation.Id);
        var tax = taxExpenseAmountsById.TryGetValue(taxExpenseId, out var expenseTax)
            ? expenseTax
            : operation.Fees
                .Where(fee => fee.Kind == FeeKind.TaxWithheld)
                .Sum(fee => fee.Amount.Value.Amount);
        Money? fees = commission > 0m ? new Money(commission, Euro) : null;
        Money? taxMoney = tax > 0m ? new Money(tax, Euro) : null;
        var accountId = operation.SettlementAccount?.Id ?? Guid.Empty;
        var accountName = operation.SettlementAccount is { } settlementAccount
            ? accounts.TryGetValue(settlementAccount.Id, out var account)
                ? account.Name
                : settlementAccount.Name
            : string.Empty;

        return new InvestmentOperationRow(
            operation.Id,
            accountId,
            operation.InvestmentKind,
            operation.OperationDate,
            operation.Quantity,
            quoted?.PricePerUnit?.Price.Amount ?? quoted?.PercentOfNominal,
            quoted?.Unit,
            effectiveUnitPrice,
            grossAmount,
            fees,
            operation.Amount?.Value,
            NormalizeDescription(operation.Description),
            accountName,
            operation.Status == OperationStatus.Reversed,
            taxMoney,
            operation.RecordedAt);
    }

    private static string? NormalizeDescription(string? description) =>
        description?
            .Replace("Cedola/Dividendo", "Cedola", StringComparison.CurrentCultureIgnoreCase)
            .Replace("Cedola / Dividendo", "Cedola", StringComparison.CurrentCultureIgnoreCase)
            .Trim();

    private static IReadOnlyList<InvestmentClassAllocation> BuildAllocation(
        IReadOnlyList<InvestmentAssetRow> rows)
    {
        var valued = rows
            .Where(row => row.Quantity is not null && row.CurrentValue is not null)
            .ToArray();
        var total = valued.Sum(row => row.CurrentValue!.Value.Amount);

        return valued
            .GroupBy(row => row.AssetClass)
            .Select(group =>
            {
                var value = group.Sum(row => row.CurrentValue!.Value.Amount);
                return new InvestmentClassAllocation(
                    group.Key,
                    new Money(value, Euro),
                    total > 0m ? value / total : null,
                    group.Count());
            })
            .OrderByDescending(allocation => allocation.Value?.Amount ?? 0m)
            .ThenBy(allocation => allocation.AssetClass)
            .ToArray();
    }

    private static Money? EffectiveUnitPrice(Instrument instrument, PriceObservation observation) =>
        observation.Unit switch
        {
            PriceUnit.PerUnit => new Money(observation.Price, instrument.QuoteCurrency),
            PriceUnit.PercentOfNominal when instrument.NotionalValue is { } notional =>
                new Money(notional.Amount * observation.Price / 100m, instrument.QuoteCurrency),
            _ => null,
        };
}
