using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class TransferRegistrationTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly OperationDate = new(2026, 3, 10);

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly OperationRegistrationService _service;

    public TransferRegistrationTests()
    {
        _service = new OperationRegistrationService(_accounts, _categories, _operations);
    }

    [Fact]
    public async Task TransferAsync_ShouldPersistBothLegsOfATransfer()
    {
        var source = RegisterAccount("Conto corrente");
        var destination = RegisterAccount("Cassa");

        var transfer = await _service.TransferAsync(new RegisterTransferRequest(
            source.Id,
            destination.Id,
            new PositiveMoney(new Money(100m, Euro)),
            new PositiveMoney(new Money(100m, Euro)),
            OperationDate));

        transfer.Kind.ShouldBe(OperationKind.Transfer);
        _operations.Operations.ShouldHaveSingleItem().ShouldBe(transfer);
        var postings = transfer.GeneratePostings();
        postings.Count.ShouldBe(2);
        SinglePostingFor(postings, source.Id).SignedAmount.ShouldBe(-100m);
        SinglePostingFor(postings, destination.Id).SignedAmount.ShouldBe(100m);
    }

    [Fact]
    public async Task TransferAsync_ShouldRejectAMissingSourceAccount()
    {
        var destination = RegisterAccount("Cassa");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.TransferAsync(new RegisterTransferRequest(
                Guid.NewGuid(),
                destination.Id,
                new PositiveMoney(new Money(100m, Euro)),
                new PositiveMoney(new Money(100m, Euro)),
                OperationDate)));

        exception.Message.ShouldContain("Il conto");
    }

    [Fact]
    public async Task TransferAsync_ShouldRejectAMissingDestinationAccount()
    {
        var source = RegisterAccount("Conto corrente");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.TransferAsync(new RegisterTransferRequest(
                source.Id,
                Guid.NewGuid(),
                new PositiveMoney(new Money(100m, Euro)),
                new PositiveMoney(new Money(100m, Euro)),
                OperationDate)));

        exception.Message.ShouldContain("Il conto");
    }

    [Fact]
    public async Task TransferAsync_ShouldRejectTheSameSourceAndDestinationAccount()
    {
        var source = RegisterAccount("Conto corrente");

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.TransferAsync(new RegisterTransferRequest(
                source.Id,
                source.Id,
                new PositiveMoney(new Money(100m, Euro)),
                new PositiveMoney(new Money(100m, Euro)),
                OperationDate)));

        exception.Message.ShouldContain("due conti distinti");
    }

    [Fact]
    public async Task TransferAsync_ShouldRejectUnbalancedLegs()
    {
        var source = RegisterAccount("Conto corrente");
        var destination = RegisterAccount("Cassa");

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.TransferAsync(new RegisterTransferRequest(
                source.Id,
                destination.Id,
                new PositiveMoney(new Money(100m, Euro)),
                new PositiveMoney(new Money(90m, Euro)),
                OperationDate)));

        exception.Message.ShouldContain("devono coincidere");
    }

    private Account RegisterAccount(string name)
    {
        var account = new Account(
            Guid.NewGuid(), name, AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(account);
        return account;
    }

    private static AccountPosting SinglePostingFor(
        IReadOnlyList<AccountPosting> postings,
        Guid accountId) =>
        postings
            .Where(posting => posting.AccountId == accountId)
            .ShouldHaveSingleItem();
}
