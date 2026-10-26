using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Expense : FinancialOperation
{
    public Expense(
        Guid id,
        Account? account,
        Category category,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null,
        Instrument? instrument = null)
        : base(id, operationDate, recordedAt, description, notes)
    {
        if (account is not null)
        {
            ValidateOperationCurrency(amount.Value, account);
        }
        else if (amount.Value.Currency != Currency.Euro)
        {
            throw new ArgumentException(
                "Le uscite senza conto devono essere in EUR.",
                nameof(amount));
        }

        Account = account;
        Category = category;
        Amount = amount;
        Instrument = instrument;
    }

    public Account? Account { get; }

    public Category Category { get; }

    public PositiveMoney Amount { get; }

    /// <summary>Strumento finanziario associato, per esempio per commissioni e tasse d'investimento.</summary>
    public Instrument? Instrument { get; }

    public override OperationKind Kind => OperationKind.Expense;

    protected override IReadOnlyList<AccountPosting> CreatePostings() =>
        Account is null
            ? []
            :
            [
                new AccountPosting(
                    this,
                    Account.Id,
                    PostingKind.Capital,
                    Account.Currency,
                    SignedAmount(Amount.Value, Account, credit: false)),
            ];
}
