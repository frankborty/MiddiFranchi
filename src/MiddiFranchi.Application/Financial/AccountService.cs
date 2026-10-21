using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed class AccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountPostingRepository _postings;

    public AccountService(IAccountRepository accounts, IAccountPostingRepository postings)
    {
        _accounts = accounts;
        _postings = postings;
    }

    public async Task<Account> CreateAsync(NewAccount request, CancellationToken cancellationToken = default)
    {
        var account = new Account(
            Guid.NewGuid(),
            request.Name,
            request.Type,
            Currency.Euro,
            AccountStatus.Active);
        await _accounts.AddAsync(account, cancellationToken);
        return account;
    }

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        _accounts.GetByIdAsync(accountId, cancellationToken);

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) =>
        _accounts.ListAsync(cancellationToken);

    /// <summary>
    /// Saldo ricostruito dai posting validi fino alla data richiesta, esclusi gli storni,
    /// sempre nella valuta del conto. Non è il saldo manuale corrente osservato: per quello
    /// si usa ManualBalanceService.
    /// </summary>
    public async Task<Money> GetCalculatedBalanceAsync(
        Guid accountId,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

        var asOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var postings = await _postings.ListForAccountAsync(accountId, asOf, cancellationToken);
        return AccountBalanceCalculator.Balance(account, postings, asOf);
    }
}
