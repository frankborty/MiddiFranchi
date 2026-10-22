using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Web.Components;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests;

/// <summary>
/// Test di coordinamento della form di registrazione: nessuna dipendenza da database,
/// sole convalide e instradamento verso i servizi Application.
/// </summary>
public class OperationFormTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly ValidAt = new(2026, 4, 15);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeOperationRegistrationRepository _operations = new();
    private readonly OperationRegistrationService _service;
    private readonly OperationFormDispatcher _dispatcher;

    public OperationFormTests()
    {
        _service = new OperationRegistrationService(_accounts, _categories, _operations);
        _dispatcher = new OperationFormDispatcher(_service);
    }

    private Account WithAccount(string name, Currency currency) =>
        new(Guid.NewGuid(), name, AccountType.Checking, currency, AccountStatus.Active);

    [Fact]
    public void CategoriesFor_ShouldFilterByTheOperationKind()
    {
        var income = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        var expense = new Category(Guid.NewGuid(), "Affitto", CategoryType.Expense);
        IReadOnlyList<Category> categories = [income, expense];

        OperationForm.CategoriesFor(OperationFormKind.Income, categories)
            .ShouldBe([income]);
        OperationForm.CategoriesFor(OperationFormKind.Expense, categories)
            .ShouldBe([expense]);
        OperationForm.CategoriesFor(OperationFormKind.Transfer, categories)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRouteIncomeToTheIncomeServiceWithoutAnAccount()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                CategoryId = category.Id,
                SourceAmountText = "123,45",
                ValidAt = ValidAt,
            });

        result.Succeeded.ShouldBeTrue();
        var registered = _operations.Operations.ShouldHaveSingleItem();
        var income = registered.ShouldBeOfType<Income>();
        income.Account.ShouldBeNull();
        income.Amount.Value.Amount.ShouldBe(123.45m);
        income.Amount.Value.Currency.Code.ShouldBe("EUR");
        income.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRouteExpenseToTheExpenseServiceWithoutAnAccount()
    {
        var category = new Category(Guid.NewGuid(), "Affitto", CategoryType.Expense);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Expense,
                CategoryId = category.Id,
                SourceAmountText = "50.00",
                ValidAt = ValidAt,
                Description = "Affitto giugno",
            });

        result.Succeeded.ShouldBeTrue();
        var expense = _operations.Operations.ShouldHaveSingleItem().ShouldBeOfType<Expense>();
        expense.Account.ShouldBeNull();
        expense.Amount.Value.Amount.ShouldBe(50m);
        expense.GeneratePostings().ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRouteASameCurrencyTransferWithMatchingLegs()
    {
        var source = WithAccount("Conto corrente", Euro);
        var destination = WithAccount("Cassa", Euro);
        _accounts.Accounts.Add(source);
        _accounts.Accounts.Add(destination);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Transfer,
                SourceAccountId = source.Id,
                DestinationAccountId = destination.Id,
                SourceAmountText = "200.00",
                ValidAt = ValidAt,
            },
            NamedAccounts(source, destination));

        result.Succeeded.ShouldBeTrue();
        var transfer = _operations.Operations.ShouldHaveSingleItem().ShouldBeOfType<Transfer>();
        transfer.SourceAmount.Value.Amount.ShouldBe(200m);
        transfer.DestinationAmount.Value.Amount.ShouldBe(200m);
        transfer.GeneratePostings().Select(posting => posting.SignedAmount)
            .ShouldBe([-200m, 200m]);
    }


    [Fact]
    public async Task SubmitAsync_ShouldRejectAMissingRequiredField()
    {
        var model = new OperationFormModel
        {
            Kind = OperationFormKind.Income,
            SourceAmountText = "10",
            ValidAt = ValidAt,
        };

        var result = await _dispatcher.SubmitAsync(model);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("categoria");
        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRejectAMissingCategory()
    {
        var model = new OperationFormModel
        {
            Kind = OperationFormKind.Income,
            SourceAmountText = "10",
            ValidAt = ValidAt,
        };

        var result = await _dispatcher.SubmitAsync(model);

        result.Error.ShouldNotBeNull().ShouldContain("categoria");
        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRejectAMissingDate()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                CategoryId = category.Id,
                SourceAmountText = "10",
            });

        result.Error.ShouldNotBeNull().ShouldContain("data economica");
        _operations.Operations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-5", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData("10,5", true)]
    [InlineData("10.5", true)]
    public async Task SubmitAsync_ShouldValidateTheAmount(string amountText, bool expectedAccepted)
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                CategoryId = category.Id,
                SourceAmountText = amountText,
                ValidAt = ValidAt,
            });

        result.Succeeded.ShouldBe(expectedAccepted, result.Error ?? "riuscita");
        if (expectedAccepted)
        {
            var income = _operations.Operations.OfType<Income>().ShouldHaveSingleItem();
            income.Amount.Value.Amount.ShouldBe(10.5m);
        }
        else
        {
            _operations.Operations.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SubmitAsync_ShouldRejectATransferWithTheSameSourceAndDestination()
    {
        var account = WithAccount("Conto corrente", Euro);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Transfer,
                SourceAccountId = account.Id,
                DestinationAccountId = account.Id,
                SourceAmountText = "10",
                ValidAt = ValidAt,
            },
            NamedAccounts(account));

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("conti distinti");
        _operations.Operations.ShouldBeEmpty();
    }



    [Fact]
    public async Task SubmitAsync_ShouldExposeTheApplicationErrorMessageWithoutStackTraces()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                CategoryId = Guid.NewGuid(),
                SourceAmountText = "10",
                ValidAt = ValidAt,
            });

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("non esiste");
        result.Error.ShouldNotContain("at MiddiFranchi");
    }

    private static IReadOnlyDictionary<Guid, Account> NamedAccounts(params Account[] accounts) =>
        accounts.ToDictionary(account => account.Id);

    /// <summary>Falsificazione minimale del repository dei conti per i test di coordinamento.</summary>
    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> Accounts { get; } = [];

        public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Accounts.FirstOrDefault(account => account.Id == id));

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Account>>(Accounts.ToArray());

        public Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
        {
            var index = Accounts.FindIndex(candidate => candidate.Id == account.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Il conto {account.Id} non esiste.");
            }

            Accounts[index] = account;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Accounts.RemoveAll(account => account.Id == id);
            return Task.CompletedTask;
        }
    }

    /// <summary>Falsificazione minimale del repository delle categorie per i test di coordinamento.</summary>
    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Categories.FirstOrDefault(category => category.Id == id));

        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Category>>(Categories.ToArray());

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            var index = Categories.FindIndex(candidate => candidate.Id == category.Id);
            if (index < 0)
            {
                throw new InvalidOperationException("Categoria non trovata.");
            }

            Categories[index] = category;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Categories.RemoveAll(category => category.Id == id);
            return Task.CompletedTask;
        }
    }

    /// <summary>Falsificazione minimale del repository delle operazioni per i test di coordinamento.</summary>
    private sealed class FakeOperationRegistrationRepository : IFinancialOperationRepository
    {
        public List<FinancialOperation> Operations { get; } = [];

        public Task<FinancialOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Operations.FirstOrDefault(operation => operation.Id == id));

        public Task<IReadOnlyList<FinancialOperation>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialOperation>>(Operations.ToArray());

        public Task<IReadOnlyList<InvestmentOperation>> ListInvestmentsByPortfolioAsync(
            Guid portfolioId,
            DateOnly asOfDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InvestmentOperation>>([]);

        public Task AddAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
        {
            Operations.Add(operation);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FinancialOperation operation, CancellationToken cancellationToken = default)
        {
            var index = Operations.FindIndex(candidate => candidate.Id == operation.Id);
            if (index < 0)
            {
                throw new InvalidOperationException("Operazione non trovata.");
            }

            Operations[index] = operation;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Operations.RemoveAll(operation => operation.Id == id || operation.OriginalOperationId == id);
            return Task.CompletedTask;
        }

        public Task<bool> AnyReferencingAccountAsync(
            Guid accountId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Operations.Any(operation => operation switch
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
            }));
    }
}
