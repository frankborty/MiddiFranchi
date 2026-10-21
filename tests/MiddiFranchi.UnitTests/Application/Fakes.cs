using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.UnitTests.Application;

public sealed class FakeManualBalanceRepository : IManualBalanceRepository
{
    public List<ManualBalance> Readings { get; } = [];

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task AddAsync(ManualBalance reading, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Readings.Add(reading);
        return Task.CompletedTask;
    }

    public Task<ManualBalance?> GetLatestAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(ManualBalances.Latest(Readings, accountId));
    }

    public Task<ManualBalance?> GetLatestOnOrBeforeAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(ManualBalances.LatestOnOrBefore(Readings, accountId, asOfDate));
    }

    public Task<IReadOnlyList<ManualBalance>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<ManualBalance> result = Readings
            .Where(reading => reading.AccountId == accountId)
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ManualBalance>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<ManualBalance> result = Readings
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToArray();
        return Task.FromResult(result);
    }
}

public sealed class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = [];

    public List<Account> Accounts => _accounts;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_accounts.FirstOrDefault(account => account.Id == id));
    }

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<Account> result = _accounts
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _accounts.Add(account);
        return Task.CompletedTask;
    }
}

public sealed class FakeCategoryRepository : ICategoryRepository
{
    private readonly List<Category> _categories = [];

    public List<Category> Categories => _categories;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_categories.FirstOrDefault(category => category.Id == id));
    }

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<Category> result = _categories
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _categories.Add(category);
        return Task.CompletedTask;
    }
}

public sealed class FakeFinancialOperationRepository : IFinancialOperationRepository
{
    private readonly List<FinancialOperation> _operations = [];

    public List<FinancialOperation> Operations => _operations;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<FinancialOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_operations.FirstOrDefault(operation => operation.Id == id));
    }

    public Task<IReadOnlyList<FinancialOperation>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<FinancialOperation> result = _operations
            .OrderBy(operation => operation.OperationDate)
            .ThenBy(operation => operation.RecordedAt)
            .ThenBy(operation => operation.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<InvestmentOperation>> ListInvestmentsByPortfolioAsync(
        Guid portfolioId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<InvestmentOperation> result = [];
        return Task.FromResult(result);
    }

    public Task AddAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _operations.Add(operation);
        return Task.CompletedTask;
    }
}

public sealed class FakeAccountPostingRepository : IAccountPostingRepository
{
    private readonly IReadOnlyList<FinancialOperation> _operations;

    public FakeAccountPostingRepository(IReadOnlyList<FinancialOperation> operations)
    {
        _operations = operations;
    }

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<IReadOnlyList<AccountPosting>> ListForAccountAsync(
        Guid accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetPostings(accountId, fromDate: null, toDate: asOfDate, cancellationToken));

    public Task<IReadOnlyList<AccountPosting>> ListAsync(
        Guid? accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(GetPostings(accountId, fromDate, toDate, cancellationToken));

    private IReadOnlyList<AccountPosting> GetPostings(
        Guid? accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<AccountPosting> result = _operations
            .SelectMany(operation => operation.GeneratePostings())
            .Where(posting => accountId is null || posting.AccountId == accountId)
            .Where(posting => fromDate is null || posting.OperationDate >= fromDate)
            .Where(posting => toDate is null || posting.OperationDate <= toDate)
            .OrderBy(posting => posting.OperationDate)
            .ThenBy(posting => posting.OperationId)
            .ThenBy(posting => posting.Kind)
            .ToArray();
        return result;
    }
}

public sealed class FakeInvestmentPortfolioRepository : IInvestmentPortfolioRepository
{
    private readonly List<InvestmentPortfolio> _portfolios = [];

    public List<InvestmentPortfolio> Portfolios => _portfolios;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<InvestmentPortfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_portfolios.FirstOrDefault(portfolio => portfolio.Id == id));
    }

    public Task<IReadOnlyList<InvestmentPortfolio>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<InvestmentPortfolio> result = _portfolios
            .OrderBy(portfolio => portfolio.Name)
            .ThenBy(portfolio => portfolio.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(InvestmentPortfolio portfolio, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _portfolios.Add(portfolio);
        return Task.CompletedTask;
    }
}

public sealed class FakePriceObservationRepository : IPriceObservationRepository
{
    private readonly List<PriceObservation> _observations = [];

    public List<PriceObservation> Observations => _observations;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<IReadOnlyList<PriceObservation>> ListForInstrumentAsync(
        Guid instrumentId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<PriceObservation> result = _observations
            .Where(observation => observation.Instrument.Id == instrumentId && observation.ValidAt <= asOfDate)
            .OrderByDescending(observation => observation.ValidAt)
            .ThenByDescending(observation => observation.ObservedAt)
            .ThenByDescending(observation => observation.Source)
            .ThenByDescending(observation => observation.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<PriceObservation>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<PriceObservation> result = _observations
            .OrderBy(observation => observation.ValidAt)
            .ThenBy(observation => observation.ObservedAt)
            .ThenBy(observation => observation.Source)
            .ThenBy(observation => observation.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(PriceObservation observation, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _observations.Add(observation);
        return Task.CompletedTask;
    }
}
