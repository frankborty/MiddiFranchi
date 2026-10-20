using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class AccountPosting
{
    public AccountPosting(
        FinancialOperation operation,
        Guid accountId,
        PostingKind kind,
        Currency currency,
        decimal signedAmount)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation), "L'operazione del posting è obbligatoria.");
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo del conto è obbligatorio.", nameof(accountId));
        }

        if (signedAmount == 0m)
        {
            throw new ArgumentException("L'importo del posting non può essere zero.", nameof(signedAmount));
        }

        if (currency.Code is null)
        {
            throw new ArgumentException("La valuta del posting è obbligatoria.", nameof(currency));
        }

        if (kind == PostingKind.Income && signedAmount < 0m)
        {
            throw new ArgumentException("Un posting di tipo Income deve avere importo positivo.", nameof(signedAmount));
        }

        if (kind == PostingKind.Fee && signedAmount > 0m)
        {
            throw new ArgumentException("Un posting di tipo Fee deve avere importo negativo.", nameof(signedAmount));
        }

        Operation = operation;
        AccountId = accountId;
        Kind = kind;
        Currency = currency;
        SignedAmount = signedAmount;
    }

    public FinancialOperation Operation { get; }

    public Guid OperationId => Operation.Id;

    public Guid AccountId { get; }

    public PostingKind Kind { get; }

    public Currency Currency { get; }

    public decimal SignedAmount { get; }

    public DateOnly OperationDate => Operation.OperationDate;
}
