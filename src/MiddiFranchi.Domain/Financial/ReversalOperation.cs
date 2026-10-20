using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class ReversalOperation : FinancialOperation
{
    public ReversalOperation(
        Guid id,
        FinancialOperation original,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null)
        : base(id, operationDate, recordedAt, description, notes, OriginalIdOf(original))
    {
        Original = original;
        original.Reverse(Id);
    }

    public FinancialOperation Original { get; }

    public override OperationKind Kind => OperationKind.Reversal;

    protected override IReadOnlyList<AccountPosting> CreatePostings() => [];

    private static Guid OriginalIdOf(FinancialOperation original)
    {
        if (original is null)
        {
            throw new ArgumentNullException(nameof(original));
        }

        if (original.Status != OperationStatus.Registered)
        {
            throw new ArgumentException(
                "L'operazione originale risulta già stornata: non si può stornare nuovamente.",
                nameof(original));
        }

        if (original is ReversalOperation)
        {
            throw new ArgumentException(
                "Uno storno non può essere stornato nella prima versione.",
                nameof(original));
        }

        return original.Id;
    }
}
