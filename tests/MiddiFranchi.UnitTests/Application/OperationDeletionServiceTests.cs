using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class OperationDeletionServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly OperationDeletionService _service;

    public OperationDeletionServiceTests()
    {
        _service = new OperationDeletionService(_operations);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTheOperationFromTheRepository()
    {
        var expense = WithExpense();

        await _service.DeleteAsync(expense.Id);

        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectAMissingOperation()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteAsync(Guid.NewGuid()));

        exception.Message.ShouldContain("Operazione non trovata");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRejectATransfer()
    {
        var source = new Account(Guid.NewGuid(), "Origine", AccountType.Checking, Euro, AccountStatus.Active);
        var destination = new Account(Guid.NewGuid(), "Destinazione", AccountType.Checking, Euro, AccountStatus.Active);
        var transfer = new Transfer(
            Guid.NewGuid(),
            source,
            destination,
            new PositiveMoney(new Money(50m, Euro)),
            new PositiveMoney(new Money(50m, Euro)),
            new DateOnly(2026, 4, 10),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.Add(transfer);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteAsync(transfer.Id));

        exception.Message.ShouldContain("Solo le entrate e le uscite");
        _operations.Operations.ShouldContain(transfer);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFlowTheCancellationToken()
    {
        var expense = WithExpense();
        using var cts = new CancellationTokenSource();

        await _service.DeleteAsync(expense.Id, cts.Token);

        _operations.ReceivedTokens.ShouldContain(cts.Token);
    }

    private Expense WithExpense()
    {
        var account = new Account(Guid.NewGuid(), "Conto", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Spesa", CategoryType.Expense, null, "Alimentari");
        var expense = new Expense(
            Guid.NewGuid(),
            account,
            category,
            new PositiveMoney(new Money(42m, Euro)),
            new DateOnly(2026, 4, 10),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.Add(expense);
        return expense;
    }
}
