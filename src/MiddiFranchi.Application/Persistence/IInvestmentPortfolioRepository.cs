using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Application.Persistence;

public interface IInvestmentPortfolioRepository
{
    Task<InvestmentPortfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestmentPortfolio>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(InvestmentPortfolio portfolio, CancellationToken cancellationToken = default);
}
