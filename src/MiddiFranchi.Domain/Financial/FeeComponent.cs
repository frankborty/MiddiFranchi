using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class FeeComponent
{
    public FeeComponent(FeeKind kind, PositiveMoney amount, Account debitAccount)
    {
        if (debitAccount is null)
        {
            throw new ArgumentNullException(nameof(debitAccount));
        }

        if (amount.Value.Currency != debitAccount.Currency)
        {
            throw new ArgumentException(
                $"Valuta della commissione ({amount.Value.Currency.Code}) diversa dalla valuta del conto di addebito ({debitAccount.Currency.Code}).",
                nameof(amount));
        }

        Kind = kind;
        Amount = amount;
        DebitAccount = debitAccount;
    }

    public FeeKind Kind { get; }

    public PositiveMoney Amount { get; }

    public Account DebitAccount { get; }
}
