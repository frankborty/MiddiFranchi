using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public abstract class FinancialOperation
{
    protected FinancialOperation(
        Guid id,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description,
        string? notes,
        Guid? originalOperationId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo dell'operazione non può essere vuoto.", nameof(id));
        }

        Id = id;
        OperationDate = operationDate;
        RecordedAt = recordedAt;
        Description = description;
        Notes = notes;
        OriginalOperationId = originalOperationId;
        Status = OperationStatus.Registered;
    }

    public Guid Id { get; }

    public DateOnly OperationDate { get; }

    public DateTimeOffset RecordedAt { get; }

    public string? Description { get; }

    public string? Notes { get; }

    public Guid? OriginalOperationId { get; private set; }

    public Guid? ReversalOperationId { get; private set; }

    public OperationStatus Status { get; private set; }

    public abstract OperationKind Kind { get; }

    public IReadOnlyList<AccountPosting> GeneratePostings() =>
        Status == OperationStatus.Registered ? CreatePostings() : [];

    protected abstract IReadOnlyList<AccountPosting> CreatePostings();

    internal void Reverse(Guid reversalOperationId)
    {
        Status = OperationStatus.Reversed;
        ReversalOperationId = reversalOperationId;
    }

    protected static void ValidateOperationCurrency(Money amount, Account account)
    {
        if (amount.Currency != account.Currency)
        {
            throw new ArgumentException(
                $"Valuta dell'operazione ({amount.Currency.Code}) diversa dalla valuta del conto ({account.Currency.Code}). In questa versione le operazioni entrata e spesa supportano solo la valuta del conto.",
                nameof(amount));
        }
    }

    protected static decimal SignedAmount(Money amount, Account account, bool credit) =>
        credit ? amount.Amount : -amount.Amount;
}
