using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Garantisce la presenza dei due conti speciali dell'applicazione: Cash e Prestiti.
/// L'operazione è idempotente e non modifica conti già esistenti.
/// </summary>
public sealed class SpecialAccountsSeeder
{
    public const string SeedKey = "seed.specialAccounts.v1";

    public static readonly Guid CashAccountId = Guid.Parse("c0c0c0c0-1111-4111-8111-111111111111");
    public static readonly Guid LoansAccountId = Guid.Parse("c0c0c0c0-2222-4222-8222-222222222222");

    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly IAppMetadataRepository _metadata;
    private readonly Func<DateOnly> _currentUtcDate;

    public SpecialAccountsSeeder(
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        IAppMetadataRepository metadata,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _accounts = accounts;
        _manualBalances = manualBalances;
        _metadata = metadata;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (await _metadata.IsSeedCompletedAsync(SeedKey, cancellationToken))
        {
            return;
        }

        await EnsureAccountAsync(CashAccountId, "Cash", AccountType.Cash, cancellationToken);
        await EnsureAccountAsync(LoansAccountId, "Prestiti", AccountType.Loans, cancellationToken);

        await _metadata.SetAsync(SeedKey, "done", cancellationToken);
    }

    private async Task EnsureAccountAsync(
        Guid accountId,
        string name,
        AccountType type,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            account = new Account(
                accountId,
                name,
                type,
                Currency.Euro,
                AccountStatus.Active);
            await _accounts.AddAsync(account, cancellationToken);
        }

        var latest = await _manualBalances.GetLatestAsync(accountId, cancellationToken);
        if (latest is not null)
        {
            return;
        }

        await _manualBalances.AddAsync(
            new ManualBalance(
                Guid.NewGuid(),
                account,
                new Money(0m, account.Currency),
                _currentUtcDate(),
                DateTimeOffset.UtcNow),
            cancellationToken);
    }
}
