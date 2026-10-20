using System.Globalization;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public static class PortfolioValuationCalculator
{
    private static readonly Currency Euro = Currency.Parse("EUR");

    public static PortfolioValuation Valuate(
        InvestmentPortfolio portfolio,
        IEnumerable<InvestmentOperation> operations,
        IEnumerable<PriceObservation> priceObservations,
        IEnumerable<ExchangeRateObservation> exchangeRateObservations,
        DateOnly valuationDate,
        ValuationPolicy? valuationPolicy = null)
    {
        if (portfolio is null)
        {
            throw new ArgumentNullException(nameof(portfolio));
        }

        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
        }

        if (priceObservations is null)
        {
            throw new ArgumentNullException(nameof(priceObservations));
        }

        if (exchangeRateObservations is null)
        {
            throw new ArgumentNullException(nameof(exchangeRateObservations));
        }

        var policy = valuationPolicy ?? ValuationPolicy.Unbounded;
        var priceList = priceObservations as IReadOnlyCollection<PriceObservation> ?? priceObservations.ToArray();
        var exchangeRateList = exchangeRateObservations as IReadOnlyCollection<ExchangeRateObservation>
            ?? exchangeRateObservations.ToArray();
        var positions = InvestmentPositionCalculator.Positions(portfolio, operations, valuationDate);
        var valuations = positions
            .Select(position => ValuatePosition(
                position, priceList, exchangeRateList, valuationDate, policy))
            .ToList();

        return new PortfolioValuation(portfolio, valuationDate, valuations);
    }

    private static InvestmentPositionValuation ValuatePosition(
        InvestmentPosition position,
        IEnumerable<PriceObservation> priceObservations,
        IEnumerable<ExchangeRateObservation> exchangeRateObservations,
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
                null,
                $"Lo strumento {instrument.Name} ({instrument.Id}) non ha una quotazione con ValidAt precedente o coincidente con la data richiesta.");
        }

        if (policy.IsPriceStale(priceObservation, valuationDate))
        {
            return InvestmentPositionValuation.Incomplete(
                position,
                valuationDate,
                priceObservation,
                null,
                $"La quotazione dello strumento {instrument.Name} ({instrument.Id}) è obsoleta: ValidAt {FormatDate(priceObservation.ValidAt)}, età {AgeInDays(priceObservation.ValidAt, valuationDate)} giorni, soglia {policy.MaximumPriceAgeDays} giorni.");
        }

        var marketValueInQuoteCurrency = ComputeMarketValueInQuoteCurrency(instrument, position.Quantity, priceObservation);
        var referenceCurrency = position.Portfolio.ReferenceCurrency;
        ExchangeRateObservation? exchangeRateObservation = null;

        if (marketValueInQuoteCurrency.Currency != referenceCurrency)
        {
            if (marketValueInQuoteCurrency.Currency != Euro && referenceCurrency != Euro)
            {
                throw new InvalidOperationException(
                    $"La coppia di valute {marketValueInQuoteCurrency.Currency.Code}/{referenceCurrency.Code} non è supportata: i cambi del modello sono espressi tra EUR e una valuta estera (docs/domain-model.md §4.3).");
            }

            var quotedCurrency = marketValueInQuoteCurrency.Currency == Euro ? referenceCurrency : marketValueInQuoteCurrency.Currency;
            exchangeRateObservation = ExchangeRateObservations
                .MostRecentOnOrBefore(exchangeRateObservations, quotedCurrency, valuationDate);

            if (exchangeRateObservation is null)
            {
                return InvestmentPositionValuation.Incomplete(
                    position,
                    valuationDate,
                    priceObservation,
                    null,
                    $"Lo strumento {instrument.Name} ({instrument.Id}) richiede un cambio con valuta estera {quotedCurrency.Code}, ma nessuna osservazione valida è disponibile alla data richiesta.");
            }

            if (policy.IsExchangeRateStale(exchangeRateObservation, valuationDate))
            {
                return InvestmentPositionValuation.Incomplete(
                    position,
                    valuationDate,
                    priceObservation,
                    exchangeRateObservation,
                    $"Il cambio {quotedCurrency.Code}/EUR è obsoleto: ValidAt {FormatDate(exchangeRateObservation.ValidAt)}, età {AgeInDays(exchangeRateObservation.ValidAt, valuationDate)} giorni, soglia {policy.MaximumExchangeRateAgeDays} giorni.");
            }
        }

        var marketValue = exchangeRateObservation is null
            ? marketValueInQuoteCurrency
            : exchangeRateObservation.Rate.Convert(marketValueInQuoteCurrency, referenceCurrency);

        return InvestmentPositionValuation.Valuate(
            position,
            valuationDate,
            priceObservation,
            exchangeRateObservation,
            marketValue);
    }

    private static Money ComputeMarketValueInQuoteCurrency(
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
