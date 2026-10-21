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
    public async Task SubmitAsync_ShouldRouteIncomeToTheIncomeService()
    {
        var account = WithAccount("Conto corrente", Euro);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                AccountId = account.Id,
                CategoryId = category.Id,
                SourceAmountText = "123,45",
                ValidAt = ValidAt,
            },
            NamedAccounts(account));

        result.Succeeded.ShouldBeTrue();
        var registered = _operations.Operations.ShouldHaveSingleItem();
        var income = registered.ShouldBeOfType<Income>();
        income.Amount.Value.Amount.ShouldBe(123.45m);
        income.Amount.Value.Currency.Code.ShouldBe("EUR");
        income.GeneratePostings().ShouldHaveSingleItem().SignedAmount.ShouldBe(123.45m);
    }

    [Fact]
    public async Task SubmitAsync_ShouldRouteExpenseToTheExpenseService()
    {
        var account = WithAccount("Conto corrente", Euro);
        var category = new Category(Guid.NewGuid(), "Affitto", CategoryType.Expense);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Expense,
                AccountId = account.Id,
                CategoryId = category.Id,
                SourceAmountText = "50.00",
                ValidAt = ValidAt,
                Description = "Affitto giugno",
                Notes = "consulente pagamento",
            },
            NamedAccounts(account));

        result.Succeeded.ShouldBeTrue();
        var expense = _operations.Operations.ShouldHaveSingleItem().ShouldBeOfType<Expense>();
        expense.Amount.Value.Amount.ShouldBe(50m);
        // Il segno resta nel posting, l'importo della spesa è positivo.
        expense.GeneratePostings().ShouldHaveSingleItem().SignedAmount.ShouldBe(-50m);
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

        var result = await _dispatcher.SubmitAsync(model, new Dictionary<Guid, Account>());

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("conto destinatario");
        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRejectAMissingCategory()
    {
        var account = WithAccount("Conto corrente", Euro);
        var model = new OperationFormModel
        {
            Kind = OperationFormKind.Income,
            AccountId = account.Id,
            SourceAmountText = "10",
            ValidAt = ValidAt,
        };

        var result = await _dispatcher.SubmitAsync(model, NamedAccounts(account));

        result.Error.ShouldNotBeNull().ShouldContain("categoria");
        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task SubmitAsync_ShouldRejectAMissingDate()
    {
        var account = WithAccount("Conto corrente", Euro);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                AccountId = account.Id,
                CategoryId = category.Id,
                SourceAmountText = "10",
            },
            NamedAccounts(account));

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
        var account = WithAccount("Conto corrente", Euro);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                AccountId = account.Id,
                CategoryId = category.Id,
                SourceAmountText = amountText,
                ValidAt = ValidAt,
            },
            NamedAccounts(account));

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
        var account = WithAccount("Conto corrente", Euro);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);

        var result = await _dispatcher.SubmitAsync(
            new OperationFormModel
            {
                Kind = OperationFormKind.Income,
                AccountId = account.Id,
                CategoryId = Guid.NewGuid(),
                SourceAmountText = "10",
                ValidAt = ValidAt,
            },
            NamedAccounts(account));

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
    }
}
