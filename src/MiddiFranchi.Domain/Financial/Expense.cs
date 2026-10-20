using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Expense : FinancialOperation
{
    public Expense(
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

    public override OperationKind Kind => OperationKind.Expense;

    protected override IReadOnlyList<AccountPosting> CreatePostings() =>
    [
        new AccountPosting(
            this,
            Account.Id,
            PostingKind.Capital,
            Account.Currency,
            SignedAmount(Amount.Value, Account, credit: false)),
    ];
}
