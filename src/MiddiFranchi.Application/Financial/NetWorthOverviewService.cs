using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Riepilogo del patrimonio netto alla data richiesta. I saldi dei conti derivano esclusivamente
/// dalle rilevazioni manuali (fonte autorevole, passata esplicitamente al calcolatore del Domain:
/// i posting non vengono letti e non si sommano mai ai saldi manuali); gli investimenti sono
/// valorizzati dal Domain con le osservazioni di prezzo registrate. Le mancanze producono un
/// riepilogo incompleto, mai valori convenzionali a zero.
/// </summary>
public sealed class NetWorthOverviewService
{
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly IFinancialOperationRepository _operations;
    private readonly IPriceObservationRepository _prices;

    public NetWorthOverviewService(
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        IFinancialOperationRepository operations,
        IPriceObservationRepository prices)
    {
        _accounts = accounts;
        _manualBalances = manualBalances;
        _operations = operations;
        _prices = prices;
    }

    public async Task<NetWorthSummary> SummarizeAsync(
        DateOnly valuationDate,
        CancellationToken cancellationToken = default)
    {
        var accountList = await _accounts.ListAsync(cancellationToken);
        var readings = await _manualBalances.ListAsync(cancellationToken);
        var operations = await _operations.ListAsync(cancellationToken);
        var priceObservations = await _prices.ListAsync(cancellationToken);

        var investmentOperations = operations
            .OfType<InvestmentOperation>()
            .ToArray();

        return NetWorthCalculator.Summarize(
            accountList,
            [],
            investmentOperations,
            priceObservations,
            valuationDate,
            ValuationPolicy.Unbounded,
            readings);
    }
}
