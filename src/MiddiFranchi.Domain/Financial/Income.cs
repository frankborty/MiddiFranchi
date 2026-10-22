using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Income : FinancialOperation
{
    public Income(
        Guid id,
        Account? account,
        Category category,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null)
        : base(id, operationDate, recordedAt, description, notes)
    {
        if (account is not null)
        {
            ValidateOperationCurrency(amount.Value, account);
        }
        else if (amount.Value.Currency != Currency.Euro)
        {
            throw new ArgumentException(
                "Le entrate senza conto devono essere in EUR.",
                nameof(amount));
        }

        Account = account;
        Category = category;
        Amount = amount;
    }

    public Account? Account { get; }

    public Category Category { get; }

    public PositiveMoney Amount { get; }

    public override OperationKind Kind => OperationKind.Income;

    protected override IReadOnlyList<AccountPosting> CreatePostings() =>
        Account is null
            ? []
            :
            [
                new AccountPosting(
                    this,
                    Account.Id,
                    PostingKind.Income,
                    Account.Currency,
                    SignedAmount(Amount.Value, Account, credit: true)),
            ];
}
