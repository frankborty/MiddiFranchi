using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.DataImport;

public sealed class AccountsImportService
{
    private readonly IAccountRepository _accounts;
    private readonly ILoanRepository _loans;
    private readonly ITransactionRunner _transactions;
    private readonly DataMaintenanceGate _gate;

    public AccountsImportService(
        IAccountRepository accounts,
        ILoanRepository loans,
        ITransactionRunner transactions,
        DataMaintenanceGate gate)
    {
        _accounts = accounts;
        _loans = loans;
        _transactions = transactions;
        _gate = gate;
    }

    public async Task<AccountsImportPreview> InspectAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var file = AccountsImportParser.Read(source);
        await ValidateAsync(file, cancellationToken).ConfigureAwait(false);
        return new AccountsImportPreview(
            file.Rows.Count,
            file.Rows.Sum(row => row.Loans.Count),
            file.Warnings,
            file.Errors);
    }

    public async Task<AccountsImportResult> ImportAsync(
        Stream source,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!string.Equals(
                confirmation,
                ImportContract.ConfirmationPhrase,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Per confermare l'importazione digita «{ImportContract.ConfirmationPhrase}».",
                nameof(confirmation));
        }

        var file = AccountsImportParser.Read(source);
        await ValidateAsync(file, cancellationToken).ConfigureAwait(false);

        if (file.Errors.Count > 0)
        {
            throw new ImportValidationException(file.Errors);
        }

        using var handle = await _gate.EnterAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await _transactions
                .RunAsync(token => ImportParsedAsync(file, token), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ImportValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            throw new ImportValidationException(
                $"Importazione non riuscita: {exception.Message}",
                exception);
        }
    }

    public Task<ImportTemplate> CreateTemplateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ImportTemplateBuilder.BuildAccountsTemplate());
    }

    private async Task ValidateAsync(
        ParsedAccountsFile file,
        CancellationToken cancellationToken)
    {
        if (file.Rows.Count == 0 && file.Errors.Count == 0)
        {
            file.Errors.Add(
                $"{ImportFileNames.Accounts}: nessuna riga dati trovata nel file.");
            return;
        }

        if (file.Errors.Count > 0)
        {
            return;
        }

        var existingByName = new Dictionary<string, Account>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var account in await _accounts.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            existingByName.TryAdd(account.Name.Trim(), account);
        }

        foreach (var row in file.Rows)
        {
            if (!existingByName.TryGetValue(row.Name, out var existing) ||
                (existing.Type != AccountType.Loans || row.Type == AccountType.Loans))
            {
                continue;
            }

            var loans = await _loans
                .ListForAccountAsync(existing.Id, cancellationToken)
                .ConfigureAwait(false);
            if (loans.Count > 0)
            {
                file.Errors.Add(
                    $"{ImportFileNames.Accounts}, riga {row.Line}: " +
                    $"il conto «{existing.Name}» ha prestiti registrati: " +
                    "non può essere convertito a un altro tipo.");
            }
        }

        if (file.Rows.Any(row => row.Type == AccountType.Loans))
        {
            file.Warnings.Add(
                "I saldi dei conti di tipo Loans vengono ricostruiti dai prestiti all'avvio.");
        }

        if (file.Rows.Any(row => row.Loans.Count > 0))
        {
            file.Warnings.Add(
                "I prestiti vengono creati con il solo nome, senza valori: " +
                "saldo e rate si registrano successivamente nell'applicazione.");
        }

        file.Warnings.Add(
            "L'importazione non elimina conti né prestiti: crea i conti e i prestiti " +
            "il cui nome non è già presente, aggiorna il resto per nome.");
    }

    private async Task<AccountsImportResult> ImportParsedAsync(
        ParsedAccountsFile file,
        CancellationToken cancellationToken)
    {
        var existingByName = new Dictionary<string, Account>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var account in await _accounts.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            existingByName.TryAdd(account.Name.Trim(), account);
        }

        var created = 0;
        var updated = 0;
        var loansCreated = 0;
        foreach (var row in file.Rows)
        {
            Account account;
            if (existingByName.TryGetValue(row.Name, out var existing))
            {
                account = new Account(
                    existing.Id,
                    existing.Name,
                    row.Type,
                    existing.Currency,
                    row.Status,
                    row.ColorHex,
                    row.IconDataUrl);
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                updated++;
            }
            else
            {
                account = new Account(
                    Guid.NewGuid(),
                    row.Name,
                    row.Type,
                    Currency.Euro,
                    row.Status,
                    row.ColorHex,
                    row.IconDataUrl);
                await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
                created++;
                existingByName[row.Name] = account;
            }

            if (row.Loans.Count == 0)
            {
                continue;
            }

            var existingBorrowers = (await _loans
                    .ListForAccountAsync(account.Id, cancellationToken)
                    .ConfigureAwait(false))
                .Select(loan => loan.Borrower.Trim())
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

            foreach (var borrower in row.Loans)
            {
                if (!existingBorrowers.Add(borrower))
                {
                    continue;
                }

                await _loans
                    .AddAsync(new Loan(Guid.NewGuid(), account, borrower, DateTimeOffset.UtcNow), cancellationToken)
                    .ConfigureAwait(false);
                loansCreated++;
            }
        }

        return new AccountsImportResult(created, updated, loansCreated);
    }
}
