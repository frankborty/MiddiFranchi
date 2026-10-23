using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Calcola il risultato economico per strumento: plusvalenze realizzate con costo medio ponderato,
/// risultato non realizzato sul valore corrente, dividendi e cedole, commissioni autonome.
/// </summary>
public static class InvestmentResultCalculator
{
    private static readonly Currency Euro = Currency.Euro;

    public static InvestmentResultSet Calculate(
        IEnumerable<InvestmentOperation> operations,
        IEnumerable<PriceObservation> priceObservations,
        DateOnly asOfDate,
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

        var operationList = operations.ToArray();
        if (operationList.Any(operation => operation is null))
        {
            throw new ArgumentNullException(
                nameof(operations),
                "La collezione delle operazioni non può contenere null.");
        }

        var duplicate = operationList
            .GroupBy(operation => operation.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"L'operazione {duplicate.Key} è presente più volte nel calcolo del risultato: la stessa entità non può essere conteggiata due volte.",
                nameof(operations));
        }

        var policy = valuationPolicy ?? ValuationPolicy.Unbounded;
        var prices = priceObservations as IReadOnlyCollection<PriceObservation> ?? priceObservations.ToArray();

        var selected = operationList
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.OperationDate <= asOfDate)
            .OrderBy(operation => operation.OperationDate)
            .ThenBy(operation => operation.RecordedAt)
            .ThenBy(operation => operation.Id)
            .ToArray();

        var states = new Dictionary<Guid, PositionState>();
        var unattributedFees = 0m;

        foreach (var operation in selected)
        {
            if (operation.Instrument is null)
            {
                if (operation.InvestmentKind != InvestmentOperationKind.StandaloneFee)
                {
                    throw new InvalidOperationException(
                        $"L'operazione d'investimento {operation.Id} non ha uno strumento.");
                }

                unattributedFees += StandaloneFee(operation);
                continue;
            }

            var instrument = operation.Instrument;
            if (!states.TryGetValue(instrument.Id, out var state))
            {
                state = new PositionState(instrument);
                states[instrument.Id] = state;
            }

            switch (operation.InvestmentKind)
            {
                case InvestmentOperationKind.Buy:
                    ApplyBuy(operation, state);
                    break;
                case InvestmentOperationKind.Sell:
                    ApplySell(operation, state);
                    break;
                case InvestmentOperationKind.Dividend:
                case InvestmentOperationKind.Coupon:
                    state.Income += operation.Amount!.Value.Value.Amount;
                    break;
                case InvestmentOperationKind.StandaloneFee:
                    state.OtherFees += StandaloneFee(operation);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Il tipo di operazione d'investimento {operation.InvestmentKind} non è supportato dal calcolo del risultato.");
            }
        }

        var assets = states.Values
            .Select(state => BuildResult(state, prices, asOfDate, policy))
            .OrderBy(result => result.Instrument.Name, StringComparer.Ordinal)
            .ThenBy(result => result.Instrument.Id)
            .ToArray();

        return new InvestmentResultSet(assets, new Money(unattributedFees, Euro));
    }

    private static void ApplyBuy(InvestmentOperation operation, PositionState state)
    {
        var quantity = operation.Quantity!.Value.Value;
        var unitPrice = operation.UnitPrice
            ?? throw new InvalidOperationException(
                $"L'acquisto {operation.Id} non ha un prezzo unitario effettivo.");
        var fees = Fees(operation, FeeKind.BuyFee, state.Instrument.QuoteCurrency);

        state.Quantity += quantity;
        state.Cost += unitPrice.Price.Amount * quantity + fees;
    }

    private static void ApplySell(InvestmentOperation operation, PositionState state)
    {
        var quantity = operation.Quantity!.Value.Value;
        if (state.Quantity <= 0m || quantity > state.Quantity)
        {
            throw new InvalidOperationException(
                $"La vendita di {quantity} unità dello strumento {state.Instrument.Name} ({state.Instrument.Id}) supera la quantità detenuta ({state.Quantity}) alla data {operation.OperationDate}: risultato non calcolabile.");
        }

        var unitPrice = operation.UnitPrice
            ?? throw new InvalidOperationException(
                $"La vendita {operation.Id} non ha un prezzo unitario effettivo.");
        var fees = Fees(operation, FeeKind.SellFee, state.Instrument.QuoteCurrency);
        var averageCost = state.Cost / state.Quantity;
        var allocatedCost = averageCost * quantity;
        var proceeds = unitPrice.Price.Amount * quantity - fees;

        state.Realized += proceeds - allocatedCost;
        state.Cost -= allocatedCost;
        state.Quantity -= quantity;

        if (state.Quantity == 0m)
        {
            state.Cost = 0m;
        }
    }

    private static decimal Fees(InvestmentOperation operation, FeeKind expectedKind, Currency currency)
    {
        var total = 0m;
        foreach (var fee in operation.Fees)
        {
            if (fee.Kind != expectedKind)
            {
                throw new InvalidOperationException(
                    $"L'operazione {operation.Id} contiene una commissione di tipo {fee.Kind}, non ammessa nel calcolo del risultato.");
            }

            if (fee.Amount.Value.Currency != currency)
            {
                throw new InvalidOperationException(
                    $"La commissione dell'operazione {operation.Id} è in {fee.Amount.Value.Currency.Code}, diversa dalla valuta dello strumento {currency.Code}.");
            }

            total += fee.Amount.Value.Amount;
        }

        return total;
    }

    private static decimal StandaloneFee(InvestmentOperation operation)
    {
        var fee = operation.Fees.SingleOrDefault(candidate => candidate.Kind == FeeKind.Standalone)
            ?? throw new InvalidOperationException(
                $"La commissione autonoma {operation.Id} non ha una componente Standalone.");
        return fee.Amount.Value.Amount;
    }

    private static InvestmentResult BuildResult(
        PositionState state,
        IReadOnlyCollection<PriceObservation> prices,
        DateOnly asOfDate,
        ValuationPolicy policy)
    {
        var currency = state.Instrument.QuoteCurrency;
        var costBasis = new Money(state.Cost, currency);
        var realized = new Money(state.Realized, currency);
        var income = new Money(state.Income, currency);
        var otherFees = new Money(state.OtherFees, currency);

        if (state.Quantity == 0m)
        {
            var zero = new Money(0m, currency);
            return new InvestmentResult(
                state.Instrument,
                null,
                costBasis,
                realized,
                income,
                otherFees,
                zero,
                zero,
                new Money(
                    realized.Amount + income.Amount - otherFees.Amount,
                    currency),
                null);
        }

        var observation = PriceObservations.MostRecentOnOrBefore(prices, state.Instrument, asOfDate);
        if (observation is null)
        {
            return new InvestmentResult(
                state.Instrument,
                new Quantity(state.Quantity),
                costBasis,
                realized,
                income,
                otherFees,
                null,
                null,
                null,
                $"Lo strumento {state.Instrument.Name} ({state.Instrument.Id}) non ha una quotazione con ValidAt precedente o coincidente con la data richiesta.");
        }

        if (policy.IsPriceStale(observation, asOfDate))
        {
            return new InvestmentResult(
                state.Instrument,
                new Quantity(state.Quantity),
                costBasis,
                realized,
                income,
                otherFees,
                null,
                null,
                null,
                $"La quotazione dello strumento {state.Instrument.Name} ({state.Instrument.Id}) è obsoleta: ValidAt {observation.ValidAt:yyyy-MM-dd}.");
        }

        var effectiveUnitPrice = EffectiveUnitPrice(state.Instrument, observation);
        var currentValue = new Money(effectiveUnitPrice.Amount * state.Quantity, currency);
        var unrealized = new Money(currentValue.Amount - state.Cost, currency);
        var total = new Money(
            realized.Amount + unrealized.Amount + income.Amount - otherFees.Amount,
            currency);

        return new InvestmentResult(
            state.Instrument,
            new Quantity(state.Quantity),
            costBasis,
            realized,
            income,
            otherFees,
            currentValue,
            unrealized,
            total,
            null);
    }

    private static Money EffectiveUnitPrice(Instrument instrument, PriceObservation observation) =>
        observation.Unit switch
        {
            PriceUnit.PerUnit => new Money(observation.Price, observation.PriceCurrency!.Value),
            PriceUnit.PercentOfNominal when instrument.NotionalValue is { } notional =>
                new Money(notional.Amount * observation.Price / 100m, instrument.QuoteCurrency),
            _ => throw new InvalidOperationException(
                $"La quotazione dello strumento {instrument.Name} ({instrument.Id}) usa un'unità di prezzo non supportata {observation.Unit}."),
        };

    private sealed class PositionState
    {
        public PositionState(Instrument instrument)
        {
            Instrument = instrument;
        }

        public Instrument Instrument { get; }

        public decimal Quantity { get; set; }

        public decimal Cost { get; set; }

        public decimal Realized { get; set; }

        public decimal Income { get; set; }

        public decimal OtherFees { get; set; }
    }
}
