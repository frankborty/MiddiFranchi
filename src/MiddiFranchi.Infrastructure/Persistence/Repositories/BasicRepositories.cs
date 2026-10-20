using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly MiddiFranchiDbContext _context;

    public AccountRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.Accounts
            .AsNoTracking()
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .ToListAsync(cancellationToken);

        return records.Select(PersistenceMapper.ToDomain).ToArray();
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        _context.Accounts.Add(PersistenceMapper.ToRecord(account));
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly MiddiFranchiDbContext _context;

    public CategoryRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);

        return records.Select(PersistenceMapper.ToDomain).ToArray();
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        _context.Categories.Add(PersistenceMapper.ToRecord(category));
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class InvestmentPortfolioRepository : IInvestmentPortfolioRepository
{
    private readonly MiddiFranchiDbContext _context;

    public InvestmentPortfolioRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<InvestmentPortfolio?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await _context.Portfolios
            .AsNoTracking()
            .SingleOrDefaultAsync(portfolio => portfolio.Id == id, cancellationToken);

        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<InvestmentPortfolio>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _context.Portfolios
            .AsNoTracking()
            .OrderBy(portfolio => portfolio.Name)
            .ThenBy(portfolio => portfolio.Id)
            .ToListAsync(cancellationToken);

        return records.Select(PersistenceMapper.ToDomain).ToArray();
    }

    public async Task AddAsync(
        InvestmentPortfolio portfolio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(portfolio);

        _context.Portfolios.Add(PersistenceMapper.ToRecord(portfolio));
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class InstrumentRepository : IInstrumentRepository
{
    private readonly MiddiFranchiDbContext _context;

    public InstrumentRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<Instrument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await _context.Instruments
            .AsNoTracking()
            .SingleOrDefaultAsync(instrument => instrument.Id == id, cancellationToken);

        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Instrument>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _context.Instruments
            .AsNoTracking()
            .OrderBy(instrument => instrument.Name)
            .ThenBy(instrument => instrument.Id)
            .ToListAsync(cancellationToken);

        return records.Select(PersistenceMapper.ToDomain).ToArray();
    }

    public async Task AddAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        _context.Instruments.Add(PersistenceMapper.ToRecord(instrument));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
