using System.Globalization;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed class ManualBalanceService
{
    private readonly IAccountRepository _accounts;
    private readonly IManualBalanceRepository _manualBalances;
    private readonly Func<DateOnly> _currentUtcDate;

    public ManualBalanceService(
        IAccountRepository accounts,
        IManualBalanceRepository manualBalances,
        Func<DateOnly>? currentUtcDateProvider = null)
    {
        _accounts = accounts;
        _manualBalances = manualBalances;
        _currentUtcDate = currentUtcDateProvider ?? (() => DateOnly.FromDateTime(DateTime.UtcNow));
    }

    /// <summary>
    /// Registra una nuova rilevazione manuale del saldo, che diventa il saldo corrente del
    /// conto sostituendo logicamente — senza eliminare — la precedente. Le rilevazioni
    /// precedenti restano nello storico. Nessuna operazione o rettifica contabile implicita.
    /// Una data economica successiva alla data corrente (UTC) è rifiutata: le rilevazioni
    /// future non sono ammesse.
    /// </summary>
    public async Task<ManualBalance> RecordAsync(NewManualBalance request, CancellationToken cancellationToken = default)
    {
        var currentUtcDate = _currentUtcDate();
        if (request.ValidAt > currentUtcDate)
        {
            throw new ArgumentException(
                $"La data della rilevazione ({FormatDate(request.ValidAt)}) è successiva alla data corrente " +
                $"({FormatDate(currentUtcDate)}): una rilevazione manuale non può avere data futura.",
                nameof(request));
        }

        var account = await _accounts.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {request.AccountId} non esiste.");

        var reading = new ManualBalance(
            Guid.NewGuid(),
            account,
            request.Amount,
            request.ValidAt,
            DateTimeOffset.UtcNow);
        await _manualBalances.AddAsync(reading, cancellationToken);
        return reading;
    }

    /// <summary>
    /// Saldo manuale corrente del conto: l'ultima rilevazione registrata. Questo è il saldo
    /// effettivo da mostrare all'utente; il saldo ricostruito dai posting è una metrica
    /// secondaria separata (AccountService.GetCalculatedBalanceAsync) e non si somma mai al
    /// saldo manuale. Il risultato null indica saldo non disponibile, non un saldo pari a zero.
    /// </summary>
    public async Task<ManualBalance?> GetCurrentBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await RequireAccountAsync(accountId, cancellationToken);
        return await _manualBalances.GetLatestAsync(accountId, cancellationToken);
    }

    /// <summary>
    /// Saldo manuale disponibile a una data: l'ultima rilevazione con ValidAt non successiva
    /// alla data richiesta, risolta per (ValidAt, RecordedAt, Id) discendenti. Il risultato
    /// null indica saldo non disponibile, non un saldo pari a zero.
    /// </summary>
    public async Task<ManualBalance?> GetBalanceOnOrBeforeAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        await RequireAccountAsync(accountId, cancellationToken);
        return await _manualBalances.GetLatestOnOrBeforeAsync(accountId, asOfDate, cancellationToken);
    }

    /// <summary>
    /// Storico completo delle rilevazioni del conto, in ordine deterministico
    /// per (ValidAt, RecordedAt, Id) crescenti.
    /// </summary>
    public async Task<IReadOnlyList<ManualBalance>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await RequireAccountAsync(accountId, cancellationToken);
        return await _manualBalances.ListForAccountAsync(accountId, cancellationToken);
    }

    private async Task<Account> RequireAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
