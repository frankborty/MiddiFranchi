using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class Transfer : FinancialOperation
{
    private static readonly Currency Euro = Currency.Parse("EUR");

    public Transfer(
        Guid id,
        Account sourceAccount,
        Account destinationAccount,
        PositiveMoney sourceAmount,
        PositiveMoney destinationAmount,
        ExchangeRate? appliedRate,
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

        if (sourceAccount.Currency == destinationAccount.Currency)
        {
            if (appliedRate is not null)
            {
                throw new ArgumentException(
                    "Il cambio applicato non è pertinente a un trasferimento tra conti nella stessa valuta.",
                    nameof(appliedRate));
            }

            if (sourceAmount.Value.Amount != destinationAmount.Value.Amount)
            {
                throw new ArgumentException(
                    "Importi di un trasferimento nella stessa valuta: le due cifre devono coincidere.",
                    nameof(destinationAmount));
            }
        }
        else
        {
            if (appliedRate is null || !RateCoversThePair(appliedRate.Value, sourceAmount.Value.Currency, destinationAmount.Value.Currency))
            {
                throw new ArgumentException(
                    "Un trasferimento tra valute differenti richiede il cambio effettivamente applicato. In questa versione il cambio è espresso tra EUR e una valuta estera, quindi la coppia dei conti deve includere EUR.",
                    nameof(appliedRate));
            }
        }

        SourceAccount = sourceAccount;
        DestinationAccount = destinationAccount;
        SourceAmount = sourceAmount;
        DestinationAmount = destinationAmount;
        AppliedRate = appliedRate;
    }

    public Account SourceAccount { get; }

    public Account DestinationAccount { get; }

    public PositiveMoney SourceAmount { get; }

    public PositiveMoney DestinationAmount { get; }

    public ExchangeRate? AppliedRate { get; }

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

    private static bool RateCoversThePair(ExchangeRate rate, Currency source, Currency destination) =>
        (source == Euro && destination == rate.Currency) ||
        (source == rate.Currency && destination == Euro);
}
