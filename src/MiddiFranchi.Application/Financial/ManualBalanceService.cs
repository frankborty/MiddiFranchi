using System.Globalization;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

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
        EnsureNotFuture(request.ValidAt, nameof(request));

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

    /// <summary>
    /// Tutte le rilevazioni registrate, in ordine deterministico per (ValidAt, RecordedAt, Id):
    /// alimenta le viste aggregate per conto senza una query per ciascun conto.
    /// </summary>
    public async Task<IReadOnlyList<ManualBalance>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _manualBalances.ListAsync(cancellationToken);

    /// <summary>
    /// Corregge importo e data economica di una rilevazione esistente; l'istante di registrazione
    /// resta quello originale. La modifica è esplicita: una nuova rilevazione continua ad aggiungere
    /// una riga.
    /// </summary>
    public async Task<ManualBalance> UpdateAsync(
        Guid readingId,
        Money amount,
        DateOnly validAt,
        CancellationToken cancellationToken = default)
    {
        EnsureNotFuture(validAt, nameof(validAt));

        var reading = await _manualBalances.GetByIdAsync(readingId, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {readingId} non esiste.");
        var updated = new ManualBalance(
            reading.Id,
            reading.Account,
            amount,
            validAt,
            reading.RecordedAt);
        await _manualBalances.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Elimina una rilevazione esistente; il saldo corrente torna alla precedente.</summary>
    public async Task DeleteAsync(Guid readingId, CancellationToken cancellationToken = default)
    {
        _ = await _manualBalances.GetByIdAsync(readingId, cancellationToken)
            ?? throw new InvalidOperationException($"La rilevazione {readingId} non esiste.");
        await _manualBalances.DeleteAsync(readingId, cancellationToken);
    }

    private async Task<Account> RequireAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

    private void EnsureNotFuture(DateOnly validAt, string parameterName)
    {
        var currentUtcDate = _currentUtcDate();
        if (validAt > currentUtcDate)
        {
            throw new ArgumentException(
                $"La data della rilevazione ({FormatDate(validAt)}) è successiva alla data corrente " +
                $"({FormatDate(currentUtcDate)}): una rilevazione manuale non può avere data futura.",
                parameterName);
        }
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
