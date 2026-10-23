using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Ricostruisce il costo residuo delle posizioni con il costo medio ponderato descritto in
/// docs/domain-model.md. Tutti gli importi sono in EUR. Gli acquisti includono le commissioni
/// d'acquisto; le vendite riducono quantità e costo in proporzione al costo medio precedente.
/// </summary>
public static class InvestmentCostBasisCalculator
{
    public static IReadOnlyList<InvestmentCostBasis> Calculate(
        IEnumerable<InvestmentOperation> operations,
        DateOnly asOfDate)
    {
        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
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
                $"L'operazione {duplicate.Key} è presente più volte nella ricostruzione del costo: la stessa entità non può essere conteggiata due volte.",
                nameof(operations));
        }

        var states = new Dictionary<Guid, PositionState>();
        var instruments = new Dictionary<Guid, Instrument>();

        var sequence = operationList
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.OperationDate <= asOfDate)
            .Where(operation => operation.InvestmentKind is InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .OrderBy(operation => operation.OperationDate)
            .ThenBy(operation => operation.RecordedAt)
            .ThenBy(operation => operation.Id);

        foreach (var operation in sequence)
        {
            var instrument = operation.Instrument
                ?? throw new InvalidOperationException(
                    $"L'operazione {operation.Id} richiede uno strumento per il calcolo del costo.");
            var state = states.TryGetValue(instrument.Id, out var existing)
                ? existing
                : new PositionState();

            if (operation.InvestmentKind == InvestmentOperationKind.Buy)
            {
                ApplyBuy(operation, instrument, state);
            }
            else
            {
                ApplySell(operation, instrument, state);
            }

            instruments[instrument.Id] = instrument;
            states[instrument.Id] = state;
        }

        return states
            .Where(pair => pair.Value.Quantity > 0m)
            .Select(pair =>
            {
                var instrument = instruments[pair.Key];
                var state = pair.Value;
                return new InvestmentCostBasis(
                    instrument,
                    new Quantity(state.Quantity),
                    new Money(state.Cost, instrument.QuoteCurrency));
            })
            .OrderBy(cost => cost.Instrument.Name, StringComparer.Ordinal)
            .ThenBy(cost => cost.Instrument.Id)
            .ToArray();
    }

    private static void ApplyBuy(
        InvestmentOperation operation,
        Instrument instrument,
        PositionState state)
    {
        var quantity = operation.Quantity!.Value.Value;
        var unitPrice = operation.UnitPrice
            ?? throw new InvalidOperationException(
                $"L'acquisto {operation.Id} non ha un prezzo unitario effettivo.");
        var cost = unitPrice.Price.Amount * quantity + BuyFees(operation, instrument);

        state.Quantity += quantity;
        state.Cost += cost;
    }

    private static void ApplySell(
        InvestmentOperation operation,
        Instrument instrument,
        PositionState state)
    {
        var quantity = operation.Quantity!.Value.Value;
        if (quantity > state.Quantity)
        {
            throw new InvalidOperationException(
                $"La vendita di {quantity} unità dello strumento {instrument.Name} ({instrument.Id}) supera la quantità detenuta ({state.Quantity}) alla data {operation.OperationDate}: costo non calcolabile.");
        }

        if (state.Quantity <= 0m)
        {
            throw new InvalidOperationException(
                $"La vendita {operation.Id} non ha una posizione positiva da ridurre.");
        }

        var averageCost = state.Cost / state.Quantity;
        state.Cost -= averageCost * quantity;
        state.Quantity -= quantity;

        if (state.Quantity == 0m)
        {
            state.Cost = 0m;
        }
    }

    private static decimal BuyFees(InvestmentOperation operation, Instrument instrument)
    {
        var total = 0m;
        foreach (var fee in operation.Fees)
        {
            if (fee.Kind != FeeKind.BuyFee)
            {
                throw new InvalidOperationException(
                    $"L'acquisto {operation.Id} contiene una commissione di tipo {fee.Kind}, non ammessa nel costo d'acquisto.");
            }

            if (fee.Amount.Value.Currency != instrument.QuoteCurrency)
            {
                throw new InvalidOperationException(
                    $"La commissione dell'acquisto {operation.Id} è in {fee.Amount.Value.Currency.Code}, diversa dalla valuta di quotazione {instrument.QuoteCurrency.Code}.");
            }

            total += fee.Amount.Value.Amount;
        }

        return total;
    }

    private sealed class PositionState
    {
        public decimal Quantity { get; set; }

        public decimal Cost { get; set; }
    }
}
