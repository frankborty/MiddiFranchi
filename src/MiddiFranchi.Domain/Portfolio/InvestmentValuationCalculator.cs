using System.Globalization;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public static class InvestmentValuationCalculator
{
    public static InvestmentValuation Valuate(
        IEnumerable<InvestmentOperation> operations,
        IEnumerable<PriceObservation> priceObservations,
        DateOnly valuationDate,
        ValuationPolicy? valuationPolicy = null)
    {
        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
        }

        if (priceObservations is null)
        {
            throw new ArgumentNullException(nameof(priceObservations));
        }

        var policy = valuationPolicy ?? ValuationPolicy.Unbounded;
        var priceList = priceObservations as IReadOnlyCollection<PriceObservation> ?? priceObservations.ToArray();
        var positions = InvestmentPositionCalculator.Positions(operations, valuationDate);
        var valuations = positions
            .Select(position => ValuatePosition(position, priceList, valuationDate, policy))
            .ToList();

        return new InvestmentValuation(valuationDate, valuations);
    }

    private static InvestmentPositionValuation ValuatePosition(
        InvestmentPosition position,
        IEnumerable<PriceObservation> priceObservations,
        DateOnly valuationDate,
        ValuationPolicy policy)
    {
        var instrument = position.Instrument;
        var priceObservation = PriceObservations.MostRecentOnOrBefore(priceObservations, instrument, valuationDate);

        if (priceObservation is null)
        {
            return InvestmentPositionValuation.Incomplete(
                position,
                valuationDate,
                null,
                $"Lo strumento {instrument.Name} ({instrument.Id}) non ha una quotazione con ValidAt precedente o coincidente con la data richiesta.");
        }

        if (policy.IsPriceStale(priceObservation, valuationDate))
        {
            return InvestmentPositionValuation.Incomplete(
                position,
                valuationDate,
                priceObservation,
                $"La quotazione dello strumento {instrument.Name} ({instrument.Id}) è obsoleta: ValidAt {FormatDate(priceObservation.ValidAt)}, età {AgeInDays(priceObservation.ValidAt, valuationDate)} giorni, soglia {policy.MaximumPriceAgeDays} giorni.");
        }

        var marketValue = ComputeMarketValue(instrument, position.Quantity, priceObservation);
        return InvestmentPositionValuation.Valuate(
            position,
            valuationDate,
            priceObservation,
            marketValue);
    }

    private static Money ComputeMarketValue(
        Instrument instrument,
        Quantity quantity,
        PriceObservation observation)
    {
        var quotedPrice = observation.Unit switch
        {
            PriceUnit.PerUnit => QuotedPrice.AtUnitPrice(
                new UnitPrice(new Money(observation.Price, observation.PriceCurrency!.Value))),
            PriceUnit.PercentOfNominal => QuotedPrice.AsPercentOfNominal(observation.Price),
            _ => throw new InvalidOperationException(
                $"La quotazione dello strumento {instrument.Name} ({instrument.Id}) usa un'unità di prezzo non supportata {observation.Unit}."),
        };

        return quotedPrice.EffectiveUnitPrice(instrument) * quantity;
    }

    private static int AgeInDays(DateOnly validAt, DateOnly valuationDate) =>
        valuationDate.DayNumber - validAt.DayNumber;

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
