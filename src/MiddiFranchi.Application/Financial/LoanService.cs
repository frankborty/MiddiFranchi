using System.Globalization;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Gestione dei prestiti del conto Prestiti. Ogni prestito ha le proprie rilevazioni; il conto
/// espone la somma dei prestiti attraverso rilevazioni manuali aggregate, ricostruite a ogni
/// modifica così che grafici, dashboard e patrimonio netto vedano un unico saldo.
/// </summary>
public sealed class LoanService
{
    private readonly ILoanRepository _loans;
    private readonly ILoanReadingRepository _readings;
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly Func<DateOnly> _currentUtcDate;

    public LoanService(
        ILoanRepository loans,
        ILoanReadingRepository readings,
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _loans = loans;
        _readings = readings;
        _accounts = accounts;
        _manualBalances = manualBalances;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<IReadOnlyList<LoanOverview>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await RequireLoansAccountAsync(accountId, cancellationToken);

        var today = _currentUtcDate();
        var loans = await _loans.ListForAccountAsync(accountId, cancellationToken);
        var readings = await _readings.ListAsync(cancellationToken);
        var readingsByLoan = readings
            .GroupBy(reading => reading.LoanId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LoanReading>)group
                .OrderBy(reading => reading.ValidAt)
                .ThenBy(reading => reading.RecordedAt)
                .ThenBy(reading => reading.Id)
                .ToArray());

        return loans
            .Select(loan =>
            {
                var loanReadings = readingsByLoan.GetValueOrDefault(loan.Id) ?? [];
                var current = LoanReadings.LatestOnOrBefore(loanReadings, loan.Id, today);
                return new LoanOverview(loan, current?.Amount, current?.ValidAt, loanReadings);
            })
            .ToArray();
    }

    /// <summary>Ricostruisce il saldo aggregato di tutti i conti Prestiti.</summary>
    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.ListAsync(cancellationToken);
        foreach (var account in accounts.Where(account => account.Type == AccountType.Loans))
        {
            await RebuildAccountBalanceAsync(account.Id, cancellationToken);
        }
    }

    public async Task<LoanOverview?> GetAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _loans.GetByIdAsync(loanId, cancellationToken);
        if (loan is null)
        {
            return null;
        }

        var readings = await _readings.ListForLoanAsync(loanId, cancellationToken);
        var current = LoanReadings.LatestOnOrBefore(readings, loanId, _currentUtcDate());
        return new LoanOverview(loan, current?.Amount, current?.ValidAt, readings);
    }

    public async Task<Loan> CreateAsync(NewLoan request, CancellationToken cancellationToken = default)
    {
        var account = await RequireLoansAccountAsync(request.AccountId, cancellationToken);

        if (request.InitialAmount is not null)
        {
            if (request.InitialDate is null)
            {
                throw new ArgumentException(
                    "La data della rilevazione iniziale è obbligatoria quando è indicato un importo.",
                    nameof(request));
            }

            EnsureNotFuture(request.InitialDate.Value, nameof(request));
        }

        var now = DateTimeOffset.UtcNow;
        var loan = new Loan(Guid.NewGuid(), account, request.Borrower, now);
        await _loans.AddAsync(loan, cancellationToken);

        if (request.InitialAmount is { } amount)
        {
            var reading = new LoanReading(
                Guid.NewGuid(),
                loan,
                amount,
                request.InitialDate!.Value,
                now);
            await _readings.AddAsync(reading, cancellationToken);
        }

        await RebuildAccountBalanceAsync(account.Id, cancellationToken);
        return loan;
    }

    public async Task<LoanReading> AddReadingAsync(
        Guid loanId,
        Money amount,
        DateOnly validAt,
        CancellationToken cancellationToken = default)
    {
        EnsureNotFuture(validAt, nameof(validAt));
        var loan = await RequireLoanAsync(loanId, cancellationToken);

        var reading = new LoanReading(Guid.NewGuid(), loan, amount, validAt, DateTimeOffset.UtcNow);
        await _readings.AddAsync(reading, cancellationToken);
        await RebuildAccountBalanceAsync(loan.AccountId, cancellationToken);
        return reading;
    }

    public async Task<LoanReading> UpdateReadingAsync(
        Guid readingId,
        Money amount,
        DateOnly validAt,
        CancellationToken cancellationToken = default)
    {
        EnsureNotFuture(validAt, nameof(validAt));

        var reading = await _readings.GetByIdAsync(readingId, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {readingId} non esiste.");
        var updated = new LoanReading(
            reading.Id,
            reading.Loan,
            amount,
            validAt,
            reading.RecordedAt);
        await _readings.UpdateAsync(updated, cancellationToken);
        await RebuildAccountBalanceAsync(reading.Loan.AccountId, cancellationToken);
        return updated;
    }

    public async Task DeleteReadingAsync(Guid readingId, CancellationToken cancellationToken = default)
    {
        var reading = await _readings.GetByIdAsync(readingId, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {readingId} non esiste.");
        await _readings.DeleteAsync(readingId, cancellationToken);
        await RebuildAccountBalanceAsync(reading.Loan.AccountId, cancellationToken);
    }

    public async Task DeleteLoanAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await RequireLoanAsync(loanId, cancellationToken);
        await _loans.DeleteAsync(loanId, cancellationToken);
        await RebuildAccountBalanceAsync(loan.AccountId, cancellationToken);
    }

    /// <summary>
    /// Ricostruisce le rilevazioni manuali aggregate del conto Prestiti: un punto per ogni data
    /// con almeno una rilevazione, più oggi. Ogni punto somma l'ultima rilevazione di ciascun
    /// prestito (un prestito senza rilevazioni contribuisce con zero).
    /// </summary>
    private async Task RebuildAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await RequireLoansAccountAsync(accountId, cancellationToken);
        var today = _currentUtcDate();
        var loans = await _loans.ListForAccountAsync(accountId, cancellationToken);
        var loanIds = loans.Select(loan => loan.Id).ToHashSet();
        var readings = (await _readings.ListAsync(cancellationToken))
            .Where(reading => loanIds.Contains(reading.LoanId))
            .ToArray();

        var dates = readings
            .Select(reading => reading.ValidAt)
            .Where(date => date <= today)
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (!dates.Contains(today))
        {
            dates.Add(today);
        }

        // RecordedAt segue la data: garantisce che il saldo corrente scelga l'ultima data
        // anche quando PostgreSQL arrotonda i timestamp alla precisione del microsecondo.
        var synthesized = dates
            .Select(date => new ManualBalance(
                Guid.NewGuid(),
                account,
                LoanReadings.TotalOnOrBefore(loans, readings, date, account.Currency),
                date,
                new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)))
            .ToArray();

        await _manualBalances.ReplaceForAccountAsync(accountId, synthesized, cancellationToken);
    }

    private async Task<Account> RequireLoansAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

        if (account.Type != AccountType.Loans)
        {
            throw new InvalidOperationException(
                $"Il conto {account.Name} non è un conto Prestiti.");
        }

        return account;
    }

    private async Task<Loan> RequireLoanAsync(Guid loanId, CancellationToken cancellationToken) =>
        await _loans.GetByIdAsync(loanId, cancellationToken)
            ?? throw new InvalidOperationException($"Il prestito {loanId} non esiste.");

    private void EnsureNotFuture(DateOnly validAt, string parameterName)
    {
        var currentUtcDate = _currentUtcDate();
        if (validAt > currentUtcDate)
        {
            throw new ArgumentException(
                $"La data della rilevazione ({FormatDate(validAt)}) è successiva alla data corrente " +
                $"({FormatDate(currentUtcDate)}): una rilevazione non può avere data futura.",
                parameterName);
        }
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
