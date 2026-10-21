using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.Financial;

public sealed class OperationRegistrationService
{
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IFinancialOperationRepository _operations;

    public OperationRegistrationService(
        IAccountRepository accounts,
        ICategoryRepository categories,
        IFinancialOperationRepository operations)
    {
        _accounts = accounts;
        _categories = categories;
        _operations = operations;
    }

    public async Task<Income> IncomeAsync(RegisterIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var account = await RequireAccountAsync(request.AccountId, cancellationToken);
        var category = await RequireCategoryAsync(
            request.CategoryId, CategoryType.Income, "un'entrata", cancellationToken);
        var income = new Income(
            Guid.NewGuid(),
            account,
            category,
            request.Amount,
            request.OperationDate,
            DateTimeOffset.UtcNow,
            request.Description,
            request.Notes);
        await _operations.AddAsync(income, cancellationToken);
        return income;
    }

    public async Task<Expense> ExpenseAsync(RegisterExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var account = await RequireAccountAsync(request.AccountId, cancellationToken);
        var category = await RequireCategoryAsync(
            request.CategoryId, CategoryType.Expense, "un'uscita", cancellationToken);
        var expense = new Expense(
            Guid.NewGuid(),
            account,
            category,
            request.Amount,
            request.OperationDate,
            DateTimeOffset.UtcNow,
            request.Description,
            request.Notes);
        await _operations.AddAsync(expense, cancellationToken);
        return expense;
    }

    public async Task<Transfer> TransferAsync(RegisterTransferRequest request, CancellationToken cancellationToken = default)
    {
        var sourceAccount = await RequireAccountAsync(request.SourceAccountId, cancellationToken);
        var destinationAccount = await RequireAccountAsync(request.DestinationAccountId, cancellationToken);
        var transfer = new Transfer(
            Guid.NewGuid(),
            sourceAccount,
            destinationAccount,
            request.SourceAmount,
            request.DestinationAmount,
            request.OperationDate,
            DateTimeOffset.UtcNow,
            request.Description,
            request.Notes);
        await _operations.AddAsync(transfer, cancellationToken);
        return transfer;
    }

    private async Task<Account> RequireAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Il conto {accountId} non esiste.");

    private async Task<Category> RequireCategoryAsync(
        Guid categoryId,
        CategoryType expectedType,
        string operationDescription,
        CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"La categoria {categoryId} non esiste.");

        if (category.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"La categoria {category.Name} non è di tipo {expectedType} e non può essere usata per {operationDescription}.");
        }

        return category;
    }
}
