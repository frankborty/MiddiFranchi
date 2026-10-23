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

    public Task<ManualBalance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(Readings.FirstOrDefault(reading => reading.Id == id));
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

    public Task UpdateAsync(ManualBalance reading, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = Readings.FindIndex(candidate => candidate.Id == reading.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"La rilevazione {reading.Id} non esiste.");
        }

        Readings[index] = reading;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Readings.RemoveAll(reading => reading.Id == id);
        return Task.CompletedTask;
    }

    public Task ReplaceForAccountAsync(
        Guid accountId,
        IReadOnlyList<ManualBalance> readings,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Readings.RemoveAll(reading => reading.AccountId == accountId);
        Readings.AddRange(readings);
        return Task.CompletedTask;
    }
}

public sealed class FakeLoanRepository : ILoanRepository
{
    public List<Loan> Loans { get; } = [];

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(Loans.FirstOrDefault(loan => loan.Id == id));
    }

    public Task<IReadOnlyList<Loan>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<Loan> result = Loans
            .OrderBy(loan => loan.Borrower)
            .ThenBy(loan => loan.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Loan>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<Loan> result = Loans
            .Where(loan => loan.AccountId == accountId)
            .OrderBy(loan => loan.Borrower)
            .ThenBy(loan => loan.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Loans.Add(loan);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Loans.RemoveAll(loan => loan.Id == loanId);
        return Task.CompletedTask;
    }
}

public sealed class FakeLoanReadingRepository : ILoanReadingRepository
{
    public List<LoanReading> Readings { get; } = [];

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<LoanReading?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(Readings.FirstOrDefault(reading => reading.Id == id));
    }

    public Task<IReadOnlyList<LoanReading>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<LoanReading> result = Readings
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<LoanReading>> ListForLoanAsync(
        Guid loanId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<LoanReading> result = Readings
            .Where(reading => reading.LoanId == loanId)
            .OrderBy(reading => reading.ValidAt)
            .ThenBy(reading => reading.RecordedAt)
            .ThenBy(reading => reading.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(LoanReading reading, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Readings.Add(reading);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LoanReading reading, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = Readings.FindIndex(candidate => candidate.Id == reading.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"La rilevazione {reading.Id} non esiste.");
        }

        Readings[index] = reading;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        Readings.RemoveAll(reading => reading.Id == id);
        return Task.CompletedTask;
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

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = _accounts.FindIndex(candidate => candidate.Id == account.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Il conto {account.Id} non esiste.");
        }

        _accounts[index] = account;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _accounts.RemoveAll(account => account.Id == id);
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

    public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = _categories.FindIndex(candidate => candidate.Id == category.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Categoria non trovata.");
        }

        _categories[index] = category;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _categories.RemoveAll(category => category.Id == id);
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

    public Task UpdateAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = _operations.FindIndex(candidate => candidate.Id == operation.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Operazione non trovata.");
        }

        _operations[index] = operation;
        return Task.CompletedTask;
    }

    public Task UpdateInvestmentAsync(
        InvestmentOperation operation,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = _operations.FindIndex(candidate => candidate.Id == operation.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("Operazione non trovata.");
        }

        _operations[index] = operation;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var operation = _operations.FirstOrDefault(candidate => candidate.Id == id);
        if (operation is not null)
        {
            var reversal = _operations.FirstOrDefault(candidate => candidate.OriginalOperationId == id);
            if (reversal is not null)
            {
                _operations.Remove(reversal);
            }

            _operations.Remove(operation);
        }

        return Task.CompletedTask;
    }

    public Task<bool> AnyReferencingAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var referenced = Operations.Any(operation => operation switch
        {
            Income income => income.Account.Id == accountId,
            Expense expense => expense.Account.Id == accountId,
            Transfer transfer =>
                transfer.SourceAccount.Id == accountId ||
                transfer.DestinationAccount.Id == accountId,
            InvestmentOperation investment =>
                investment.SettlementAccount.Id == accountId ||
                investment.Fees.Any(fee => fee.DebitAccount.Id == accountId),
            _ => false,
        });
        return Task.FromResult(referenced);
    }

    public Task<bool> AnyForInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_operations
            .OfType<InvestmentOperation>()
            .Any(operation => operation.Instrument?.Id == instrumentId));
    }
}

public sealed class FakeInstrumentRepository : IInstrumentRepository
{
    private readonly List<Instrument> _instruments = [];

    public List<Instrument> Instruments => _instruments;

    public List<CancellationToken> ReceivedTokens { get; } = [];

    public Task<Instrument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_instruments.FirstOrDefault(instrument => instrument.Id == id));
    }

    public Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        IReadOnlyList<Instrument> result = _instruments
            .OrderBy(instrument => instrument.Name)
            .ThenBy(instrument => instrument.Id)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _instruments.Add(instrument);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var index = _instruments.FindIndex(candidate => candidate.Id == instrument.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Lo strumento {instrument.Id} non esiste.");
        }

        _instruments[index] = instrument;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _instruments.RemoveAll(instrument => instrument.Id == id);
        return Task.CompletedTask;
    }

    public Task<bool> IsinExistsAsync(
        string isin,
        Guid? excludingId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var normalized = isin.Trim().ToUpperInvariant();
        return Task.FromResult(_instruments.Any(instrument =>
            instrument.Isin is not null &&
            string.Equals(instrument.Isin, normalized, StringComparison.OrdinalIgnoreCase) &&
            (excludingId is null || instrument.Id != excludingId.Value)));
    }

    public Task<bool> SymbolExistsAsync(
        string symbol,
        Guid? excludingId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        var normalized = symbol.Trim();
        return Task.FromResult(_instruments.Any(instrument =>
            instrument.Symbol is not null &&
            string.Equals(instrument.Symbol, normalized, StringComparison.OrdinalIgnoreCase) &&
            (excludingId is null || instrument.Id != excludingId.Value)));
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

    public Task<bool> AnyForInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        return Task.FromResult(_observations.Any(observation => observation.Instrument.Id == instrumentId));
    }

    public Task AddAsync(PriceObservation observation, CancellationToken cancellationToken = default)
    {
        ReceivedTokens.Add(cancellationToken);
        _observations.Add(observation);
        return Task.CompletedTask;
    }
}
