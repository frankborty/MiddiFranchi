using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public static class AccountBalanceCalculator
{
    public static Money Balance(Account account, IEnumerable<AccountPosting> postings, DateOnly asOfDate)
    {
        if (account is null)
        {
            throw new ArgumentNullException(nameof(account));
        }

        if (postings is null)
        {
            throw new ArgumentNullException(nameof(postings));
        }

        var sum = 0m;

        foreach (var posting in postings)
        {
            if (posting.AccountId != account.Id)
            {
                continue;
            }

            if (posting.Operation.Status != OperationStatus.Registered)
            {
                continue;
            }

            if (posting.Currency != account.Currency)
            {
                throw new InvalidOperationException(
                    $"Il posting considera la valuta {posting.Currency.Code}, diversa dalla valuta del conto {account.Currency.Code}: il saldo non può essere calcolato senza conversioni implicite.");
            }

            if (posting.OperationDate <= asOfDate)
            {
                sum += posting.SignedAmount;
            }
        }

        return new Money(sum, account.Currency);
    }
}
