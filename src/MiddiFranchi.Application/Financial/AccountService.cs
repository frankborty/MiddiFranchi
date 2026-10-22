using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

public sealed class AccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountPostingRepository _postings;
    private readonly IFinancialOperationRepository _operations;
    private readonly ILoanRepository _loans;

    public AccountService(
        IAccountRepository accounts,
        IAccountPostingRepository postings,
        IFinancialOperationRepository operations,
        ILoanRepository loans)
    {
        _accounts = accounts;
        _postings = postings;
        _operations = operations;
        _loans = loans;
    }

    public async Task<Account> CreateAsync(NewAccount request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var existing = await _accounts.ListAsync(cancellationToken);
        if (existing.Any(account =>
                string.Equals(account.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException("Esiste già un conto con questo nome.");
        }

        var account = new Account(
            Guid.NewGuid(),
            name,
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
    /// Aggiorna nome, tipo, stato, colore e icona del conto. Un conto Prestiti con prestiti
    /// registrati non può essere convertito a un altro tipo.
    /// </summary>
    public async Task<Account> UpdateAsync(
        UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {request.AccountId} non esiste.");

        if (account.Type == AccountType.Loans && request.Type != AccountType.Loans)
        {
            var loans = await _loans.ListForAccountAsync(account.Id, cancellationToken);
            if (loans.Count > 0)
            {
                throw new InvalidOperationException(
                    "Il conto ha prestiti registrati: non può essere convertito a un altro tipo.");
            }
        }

        var updated = new Account(
            account.Id,
            request.Name,
            request.Type,
            account.Currency,
            request.Status,
            request.ColorHex,
            request.IconDataUrl);
        await _accounts.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Elimina il conto e le sue rilevazioni manuali. Un conto con operazioni registrate non può
    /// essere eliminato: lo storico finanziario deve restare consultabile.
    /// </summary>
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        _ = await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

        if (await _operations.AnyReferencingAccountAsync(accountId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Il conto ha operazioni registrate e non può essere eliminato: lo storico finanziario deve restare consultabile.");
        }

        await _accounts.DeleteAsync(accountId, cancellationToken);
    }

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
