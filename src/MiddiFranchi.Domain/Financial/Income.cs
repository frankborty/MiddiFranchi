using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Income : FinancialOperation
{
    public Income(
        Guid id,
        Account account,
        Category category,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null)
        : base(id, operationDate, recordedAt, description, notes)
    {
        ValidateOperationCurrency(amount.Value, account);
        Account = account;
        Category = category;
        Amount = amount;
    }

    public Account Account { get; }

    public Category Category { get; }

    public PositiveMoney Amount { get; }

    public override OperationKind Kind => OperationKind.Income;

    protected override IReadOnlyList<AccountPosting> CreatePostings() =>
    [
        new AccountPosting(
            this,
            Account.Id,
            PostingKind.Income,
            Account.Currency,
            SignedAmount(Amount.Value, Account, credit: true)),
    ];
}
