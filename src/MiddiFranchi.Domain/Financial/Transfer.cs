using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Transfer : FinancialOperation
{
    public Transfer(
        Guid id,
        Account sourceAccount,
        Account destinationAccount,
        PositiveMoney sourceAmount,
        PositiveMoney destinationAmount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null)
        : base(id, operationDate, recordedAt, description, notes)
    {
        if (sourceAccount.Id == destinationAccount.Id)
        {
            throw new ArgumentException("Un trasferimento richiede due conti distinti.", nameof(destinationAccount));
        }

        if (sourceAmount.Value.Currency != sourceAccount.Currency)
        {
            throw new ArgumentException(
                $"Valuta dell'importo in uscita ({sourceAmount.Value.Currency.Code}) diversa dalla valuta del conto di origine ({sourceAccount.Currency.Code}).",
                nameof(sourceAmount));
        }

        if (destinationAmount.Value.Currency != destinationAccount.Currency)
        {
            throw new ArgumentException(
                $"Valuta dell'importo in entrata ({destinationAmount.Value.Currency.Code}) diversa dalla valuta del conto di destinazione ({destinationAccount.Currency.Code}).",
                nameof(destinationAmount));
        }

        if (sourceAmount.Value.Amount != destinationAmount.Value.Amount)
        {
            throw new ArgumentException(
                "I due importi di un trasferimento in EUR devono coincidere.",
                nameof(destinationAmount));
        }

        SourceAccount = sourceAccount;
        DestinationAccount = destinationAccount;
        SourceAmount = sourceAmount;
        DestinationAmount = destinationAmount;
    }

    public Account SourceAccount { get; }

    public Account DestinationAccount { get; }

    public PositiveMoney SourceAmount { get; }

    public PositiveMoney DestinationAmount { get; }

    public override OperationKind Kind => OperationKind.Transfer;

    protected override IReadOnlyList<AccountPosting> CreatePostings() =>
    [
        new AccountPosting(
            this,
            SourceAccount.Id,
            PostingKind.TransferLeg,
            SourceAccount.Currency,
            SignedAmount(SourceAmount.Value, SourceAccount, credit: false)),
        new AccountPosting(
            this,
            DestinationAccount.Id,
            PostingKind.TransferLeg,
            DestinationAccount.Currency,
            SignedAmount(DestinationAmount.Value, DestinationAccount, credit: true)),
    ];
}
