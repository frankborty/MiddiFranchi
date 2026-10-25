using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Aggiunge alle quotazioni storiche il prezzo di esecuzione di acquisti e vendite. Senza questo
/// punto il grafico partirebbe solo dalla prima quotazione registrata, anche se la posizione è
/// stata aperta mesi prima.
/// </summary>
internal static class InvestmentHistoryPrices
{
    public static IReadOnlyList<PriceObservation> WithExecutionPrices(
        IReadOnlyList<PriceObservation> prices,
        IReadOnlyList<InvestmentOperation> operations)
    {
        var executionPrices = operations
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.InvestmentKind is
                InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .Select(Create)
            .Where(observation => observation is not null)
            .Select(observation => observation!)
            .ToArray();

        return executionPrices.Length == 0
            ? prices
            : prices.Concat(executionPrices).ToArray();
    }

    private static PriceObservation? Create(InvestmentOperation operation)
    {
        if (operation.Instrument is not { } instrument ||
            operation.UnitPrice is not { } unitPrice)
        {
            return null;
        }

        if (instrument.PriceUnit == PriceUnit.PerUnit)
        {
            return PriceObservation.AtUnitPrice(
                Guid.NewGuid(),
                instrument,
                unitPrice,
                operation.OperationDate,
                operation.RecordedAt,
                "Operazione");
        }

        if (operation.QuotedPrice is { PercentOfNominal: { } percent })
        {
            return PriceObservation.AsPercentOfNominal(
                Guid.NewGuid(),
                instrument,
                percent,
                operation.OperationDate,
                operation.RecordedAt,
                "Operazione");
        }

        return null;
    }
}
