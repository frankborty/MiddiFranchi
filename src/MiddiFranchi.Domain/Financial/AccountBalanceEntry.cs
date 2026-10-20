using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed record AccountBalanceEntry
{
    public AccountBalanceEntry(
        Account account,
        Money balance,
        Money? convertedBalance,
        ExchangeRateObservation? exchangeRateObservation,
        string? incompletenessReason)
    {
        Account = account;
        Balance = balance;
        ConvertedBalance = convertedBalance;
        ExchangeRateObservation = exchangeRateObservation;
        IncompletenessReason = incompletenessReason;
    }

    public Account Account { get; }

    /// <summary>Saldo firmato alla data richiesta, nella valuta del conto.</summary>
    public Money Balance { get; }

    /// <summary>Saldo convertito nella valuta di rendicontazione, oppure assente quando la conversione non è possibile.</summary>
    public Money? ConvertedBalance { get; }

    public ExchangeRateObservation? ExchangeRateObservation { get; }

    public string? IncompletenessReason { get; }

    public bool IsComplete => ConvertedBalance is not null;
}
