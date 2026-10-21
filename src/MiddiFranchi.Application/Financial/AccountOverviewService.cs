using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed record AccountOverviewRow(
    Guid AccountId,
    string Name,
    AccountType Type,
    Currency Currency,
    AccountStatus Status,
    ManualBalance? ManualReading,
    Money? CalculatedBalance)
{
    /// <summary>Saldo manuale corrente: informazione principale, assente se non registrata (mai zero per convenzione).</summary>
    public Money? ManualBalance => ManualReading?.Amount;
}

public sealed class AccountOverviewService
{
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly IAccountPostingRepository _postings;

    public AccountOverviewService(
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        IAccountPostingRepository postings)
    {
        _accounts = accounts;
        _manualBalances = manualBalances;
        _postings = postings;
    }

    /// <summary>
    /// Consultazione dei conti con entrambe le semantiche distinte: il saldo manuale corrente
    /// (ultima rilevazione registrata, null = non disponibile) come informazione principale e
    /// il saldo ricostruito dai posting come metrica secondaria, sempre nella valuta del conto.
    /// Le due grandezze non si sommano mai.
    /// </summary>
    public async Task<IReadOnlyList<AccountOverviewRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.ListAsync(cancellationToken);
        var readings = await _manualBalances.ListAsync(cancellationToken);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var postings = await _postings.ListAsync(null, null, asOf, cancellationToken);
        var postingsByAccount = postings
            .GroupBy(posting => posting.AccountId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AccountPosting>)group.ToArray());

        var rows = new List<AccountOverviewRow>(accounts.Count);
        foreach (var account in accounts)
        {
            var manualReading = ManualBalances.Latest(readings, account.Id);
            var accountPostings = postingsByAccount.GetValueOrDefault(account.Id) ?? [];
            var calculated = AccountBalanceCalculator.Balance(account, accountPostings, asOf);
            rows.Add(new AccountOverviewRow(
                account.Id,
                account.Name,
                account.Type,
                account.Currency,
                account.Status,
                manualReading,
                calculated));
        }

        return rows;
    }

    /// <summary>
    /// Consultazione di un solo conto, con la stessa semantica dell'elenco.
    /// Restituisce null per un conto inesistente.
    /// </summary>
    public async Task<AccountOverviewRow?> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var manualReading = await _manualBalances.GetLatestAsync(accountId, cancellationToken);
        var calculated = await CalculatedBalanceAsync(account, cancellationToken);
        return new AccountOverviewRow(
            account.Id,
            account.Name,
            account.Type,
            account.Currency,
            account.Status,
            manualReading,
            calculated);
    }

    /// <summary>
    /// Saldo ricostruito dai posting validi fino a oggi: metrica secondaria, mai sommata
    /// al saldo manuale e mai in conversione implicita.
    /// </summary>
    private async Task<Money> CalculatedBalanceAsync(Account account, CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var postings = await _postings.ListForAccountAsync(account.Id, asOf, cancellationToken);
        return AccountBalanceCalculator.Balance(account, postings, asOf);
    }
}
