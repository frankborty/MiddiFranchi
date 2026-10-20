using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Persistence;

public interface IFinancialOperationRepository
{
    Task<FinancialOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialOperation>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestmentOperation>> ListInvestmentsByPortfolioAsync(
        Guid portfolioId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task AddAsync(FinancialOperation operation, CancellationToken cancellationToken = default);
}
