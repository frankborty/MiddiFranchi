using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class ReversalServiceTests
{
    private static readonly Currency Euro = Currency.Euro;

    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly ReversalService _service;

    public ReversalServiceTests()
    {
        _service = new ReversalService(_operations);
    }

    private Income WithRegisteredIncome(decimal amount = 120m)
    {
        var account = new Account(Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
        var category = new Category(Guid.NewGuid(), "Stipendio", CategoryType.Income, null);
        _accounts.Accounts.Add(account);
        _categories.Categories.Add(category);
        var income = new Income(
            Guid.NewGuid(), account, category,
            new PositiveMoney(new Money(amount, Euro)),
            new DateOnly(2026, 4, 10),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.Add(income);
        return income;
    }

    private Transfer WithRegisteredTransfer()
    {
        var source = new Account(Guid.NewGuid(), "Conto EUR", AccountType.Checking, Euro, AccountStatus.Active);
        var destination = new Account(Guid.NewGuid(), "Conto destinazione", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(source);
        _accounts.Accounts.Add(destination);
        var transfer = new Transfer(
            Guid.NewGuid(), source, destination,
            new PositiveMoney(new Money(100m, Euro)),
            new PositiveMoney(new Money(100m, Euro)),
            new DateOnly(2026, 4, 10),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.Add(transfer);
        return transfer;
    }

    [Fact]
    public async Task ReverseAsync_ShouldLinkTheReversalToTheOriginalOperation()
    {
        var income = WithRegisteredIncome();

        var reversal = await _service.ReverseAsync(income.Id);

        reversal.Kind.ShouldBe(OperationKind.Reversal);
        reversal.Original.ShouldBe(income);
        reversal.OriginalOperationId.ShouldBe(income.Id);
        reversal.Status.ShouldBe(OperationStatus.Registered);
        // Lo storno non genera movimenti propri.
        reversal.GeneratePostings().ShouldBeEmpty();
        income.Status.ShouldBe(OperationStatus.Reversed);
        // L'originale resta registrato e lo storno viene aggiunto allo storico.
        _operations.Operations.Count.ShouldBe(2);
        _operations.Operations.ShouldContain(reversal);
        _operations.Operations.ShouldContain(income);
        reversal.OperationDate.ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow.Date));
    }

    [Fact]
    public async Task ReverseAsync_ShouldCoverBothLegsOfATransfer()
    {
        var transfer = WithRegisteredTransfer();

        var reversal = await _service.ReverseAsync(transfer.Id);

        reversal.Status.ShouldBe(OperationStatus.Registered);
        reversal.GeneratePostings().ShouldBeEmpty();
        // La compensazione non crea movimenti: gli effetti scompaiono dalla vista corrente
        // perché l'originale non è più Registered.
        transfer.GeneratePostings().ShouldBeEmpty();
        transfer.Status.ShouldBe(OperationStatus.Reversed);
    }

    [Fact]
    public async Task ReverseAsync_ShouldThrowForAMissingOperation()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ReverseAsync(Guid.NewGuid()));

        exception.Message.ShouldContain("non esiste");
    }

    [Fact]
    public async Task ReverseAsync_ShouldNotReverseTheSameOperationTwice()
    {
        var income = WithRegisteredIncome();
        await _service.ReverseAsync(income.Id);

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.ReverseAsync(income.Id));

        exception.Message.ShouldContain("risulta già stornata");
    }

    [Fact]
    public async Task ReverseAsync_ShouldNotReverseAReversal()
    {
        var income = WithRegisteredIncome();
        var reversal = await _service.ReverseAsync(income.Id);

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.ReverseAsync(reversal.Id));

        exception.Message.ShouldContain("Uno storno non può essere stornato");
    }

    [Fact]
    public async Task History_ShouldExposeReversibilityAndLinks()
    {
        var income = WithRegisteredIncome();
        var historyService = new OperationHistoryService(_operations, _categories);

        var before = (await historyService.ListAsync()).ShouldHaveSingleItem();
        before.IsReversible.ShouldBeTrue();
        before.IsReversed.ShouldBeFalse();
        before.IsReversal.ShouldBeFalse();
        before.Amount.ShouldNotBeNull();
        before.Amount!.Value.Amount.ShouldBe(120m);
        before.AccountLabel.ShouldBe("Conto corrente");
        before.CategoryLabel.ShouldBe("Stipendio");

        await _service.ReverseAsync(income.Id);

        var rows = await historyService.ListAsync();
        var originalRow = rows.ShouldHaveSingleItem(row => row.Id == income.Id);
        originalRow.IsReversed.ShouldBeTrue();
        originalRow.IsReversible.ShouldBeFalse();
        var reversalRow = rows.ShouldHaveSingleItem(row => row.IsReversal);
        reversalRow.OriginalOperationId.ShouldBe(income.Id);
        reversalRow.IsReversed.ShouldBeFalse();
        reversalRow.IsReversible.ShouldBeFalse();
    }

    [Fact]
    public async Task History_ShouldExposeTransferAccountsAndAmount()
    {
        WithRegisteredTransfer();
        var historyService = new OperationHistoryService(_operations, _categories);

        var row = (await historyService.ListAsync()).ShouldHaveSingleItem();

        row.Kind.ShouldBe(OperationKind.Transfer);
        row.Amount.ShouldNotBeNull();
        row.Amount!.Value.Amount.ShouldBe(100m);
        row.AccountLabel.ShouldBe("Conto EUR → Conto destinazione");
        row.CategoryLabel.ShouldBeNull();
    }

    [Fact]
    public async Task History_ShouldExcludeInvestmentOperations()
    {
        var instrument = new Domain.Portfolio.Instrument(
            Guid.NewGuid(), "ETF", Domain.Portfolio.AssetClass.Equity, Euro, Domain.Portfolio.PriceUnit.PerUnit);
        var settlement = new Account(Guid.NewGuid(), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);
        var buy = InvestmentOperation.Buy(
            Guid.NewGuid(), instrument, settlement,
            new Domain.Shared.Quantity(1m),
            Domain.Portfolio.QuotedPrice.AtUnitPrice(new Domain.Shared.UnitPrice(new Money(80m, Euro))),
            null,
            new DateOnly(2026, 4, 10),
            new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        _operations.Operations.Add(buy);
        WithRegisteredIncome();
        var historyService = new OperationHistoryService(_operations, _categories);

        var rows = await historyService.ListAsync();

        rows.Count.ShouldBe(1);
        rows.ShouldHaveSingleItem().Kind.ShouldBe(OperationKind.Income);
    }
}

// ---- piccola estensione locale per ShouldHaveSingleItem con predicato ----
public static class ReversalServiceTestExtensions
{
    public static T ShouldHaveSingleItem<T>(this IEnumerable<T> items, Func<T, bool> predicate) =>
        items.Single(predicate);
}
