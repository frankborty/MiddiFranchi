using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed record AccountBalanceEntry
{
    public AccountBalanceEntry(
        Account account,
        Money? balance,
        string? incompletenessReason)
    {
        Account = account;
        Balance = balance;
        IncompletenessReason = incompletenessReason;
    }

    public Account Account { get; }

    /// <summary>
    /// Saldo firmato in EUR alla data richiesta. Assente quando il saldo non è determinabile:
    /// per esempio, un conto senza rilevazione manuale valida nella modalità dei saldi manuali,
    /// che non diventa mai convenzionalmente zero.
    /// </summary>
    public Money? Balance { get; }

    public string? IncompletenessReason { get; }

    public bool IsComplete => Balance is not null;
}
