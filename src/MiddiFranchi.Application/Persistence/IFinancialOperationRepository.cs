using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface IFinancialOperationRepository
{
    Task<FinancialOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialOperation>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(FinancialOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna categoria, importo, data e descrizione di un'entrata o di un'uscita.</summary>
    Task UpdateAsync(FinancialOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un'operazione d'investimento, comprese commissioni e posting.
    /// </summary>
    Task UpdateInvestmentAsync(InvestmentOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Rimuove definitivamente l'operazione, i suoi posting e l'eventuale storno collegato.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Indica se almeno un'operazione, anche stornata, riferisce il conto.</summary>
    Task<bool> AnyReferencingAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Indica se almeno un'operazione d'investimento riferisce lo strumento.</summary>
    Task<bool> AnyForInstrumentAsync(Guid instrumentId, CancellationToken cancellationToken = default);
}
