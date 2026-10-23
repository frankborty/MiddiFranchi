using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public static class InvestmentPositionCalculator
{
    public static IReadOnlyList<InvestmentPosition> Positions(
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

        var duplicateOperation = operationList
            .GroupBy(operation => operation.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateOperation is not null)
        {
            throw new ArgumentException(
                $"L'operazione {duplicateOperation.Key} è presente più volte nella ricostruzione delle posizioni: la stessa entità non può essere conteggiata due volte.",
                nameof(operations));
        }

        var heldQuantity = new Dictionary<Guid, decimal>();
        var instruments = new Dictionary<Guid, Instrument>();

        var sequence = operationList
            .Where(operation => operation.Status == OperationStatus.Registered)
            .Where(operation => operation.OperationDate <= asOfDate)
            .Where(operation =>
                operation.InvestmentKind is InvestmentOperationKind.Buy or InvestmentOperationKind.Sell)
            .OrderBy(operation => operation.OperationDate)
            .ThenBy(operation => operation.RecordedAt)
            .ThenBy(operation => operation.Id);

        foreach (var operation in sequence)
        {
            var instrument = operation.Instrument!;
            var delta = operation.InvestmentKind == InvestmentOperationKind.Buy
                ? operation.Quantity!.Value.Value
                : -operation.Quantity!.Value.Value;

            heldQuantity.TryGetValue(instrument.Id, out var currentQuantity);
            var updated = currentQuantity + delta;

            if (updated < 0m)
            {
                throw new InvalidOperationException(
                    $"La vendita di {Math.Abs(delta)} unità dello strumento {instrument.Name} ({instrument.Id}) supera la quantità detenuta ({currentQuantity}) alla data {operation.OperationDate}: posizione non valida.");
            }

            instruments[instrument.Id] = instrument;
            heldQuantity[instrument.Id] = updated;
        }

        return heldQuantity
            .Where(entry => entry.Value > 0m)
            .Select(entry => new InvestmentPosition(instruments[entry.Key], new Quantity(entry.Value)))
            .OrderBy(position => position.Instrument.Id)
            .ToList();
    }
}
