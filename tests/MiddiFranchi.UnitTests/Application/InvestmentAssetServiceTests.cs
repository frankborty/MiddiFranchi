using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class InvestmentAssetServiceTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly FakeInstrumentRepository _instruments = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakePriceObservationRepository _prices = new();
    private readonly InvestmentAssetService _service;
    private readonly Account _account;

    public InvestmentAssetServiceTests()
    {
        _account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(_account);
        _categories.Categories.Add(new Category(
            Guid.NewGuid(), "Fees", CategoryType.Expense, subcategory: "Tasse Investimenti"));
        _categories.Categories.Add(new Category(
            Guid.NewGuid(), "Fees", CategoryType.Expense, subcategory: "Conto Titoli"));
        _service = new InvestmentAssetService(
            _instruments,
            _operations,
            _prices,
            _categories,
            new FakeTransactionRunner(),
            () => Today);
    }

    [Fact]
    public async Task Create_ShouldNormalizeIdentityAndPersistTheInstrument()
    {
        var created = await CreateAsync(
            symbol: "  v3ma  ",
            isin: "  ie00b4l5y983  ");

        created.Isin.ShouldBe("IE00B4L5Y983");
        created.Symbol.ShouldBe("v3ma");
        created.Status.ShouldBe(InstrumentStatus.Active);
        created.TaxRate.ShouldBe(Instrument.DefaultTaxRate);
        _instruments.Instruments.ShouldHaveSingleItem().ShouldBe(created);
    }

    [Fact]
    public async Task Create_ShouldPersistTheConfiguredTaxRate()
    {
        var created = await CreateAsync(taxRate: 12.5m);

        created.TaxRate.ShouldBe(12.5m);
        _instruments.Instruments.ShouldHaveSingleItem().TaxRate.ShouldBe(12.5m);
    }

    [Fact]
    public async Task Create_ShouldRejectADuplicateIsin()
    {
        await CreateAsync(isin: "IE00B4L5Y983");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAsync(isin: "ie00b4l5y983"));

        exception.Message.ShouldContain("ISIN");
        _instruments.Instruments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Create_ShouldRejectADuplicateSymbol()
    {
        await CreateAsync(symbol: "V3MA");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAsync(symbol: "v3ma"));

        exception.Message.ShouldContain("sigla");
        _instruments.Instruments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RegisterBuy_ShouldPersistTheOperation()
    {
        var instrument = await CreateAsync();

        var buy = await BuyAsync(instrument, quantity: 2m, price: 10m);

        buy.InvestmentKind.ShouldBe(InvestmentOperationKind.Buy);
        buy.Instrument.ShouldNotBeNull().Id.ShouldBe(instrument.Id);
        buy.SettlementAccount.ShouldBeNull();
        buy.Quantity!.Value.Value.ShouldBe(2m);
        _operations.Operations.ShouldContain(buy);
    }

    [Fact]
    public async Task RegisterSell_ShouldRejectAQuantityAboveTheHeldPosition()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 1m, price: 10m);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => SellAsync(instrument, quantity: 2m, price: 12m));

        exception.Message.ShouldContain("supera");
        _operations.Operations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RegisterDividend_ShouldPersistIncome()
    {
        var instrument = await CreateAsync();

        var dividend = await _service.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            InvestmentOperationKind.Dividend,
            Today,
            Amount: 12.5m));

        dividend.Amount.ShouldNotBeNull().Value.Amount.ShouldBe(12.5m);
        dividend.InvestmentKind.ShouldBe(InvestmentOperationKind.Dividend);
    }

    [Fact]
    public async Task UpdateOperation_ShouldKeepIdentityAndReplaceTheOperation()
    {
        var instrument = await CreateAsync();
        var buy = await BuyAsync(instrument, quantity: 2m, price: 10m);

        var updated = await _service.UpdateOperationAsync(new UpdateInvestmentOperationRequest(
            buy.Id,
            Today,
            Quantity: 3m,
            Price: 12m));

        updated.Id.ShouldBe(buy.Id);
        updated.RecordedAt.ShouldBe(buy.RecordedAt);
        updated.Quantity!.Value.Value.ShouldBe(3m);
        updated.UnitPrice!.Value.Price.ShouldBe(new Money(12m, Euro));
        _operations.Operations.ShouldHaveSingleItem().ShouldBe(updated);
    }

    [Fact]
    public async Task DeleteOperation_ShouldRemoveIt()
    {
        var instrument = await CreateAsync();
        var buy = await BuyAsync(instrument);

        await _service.DeleteOperationAsync(buy.Id);

        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Archive_ShouldRejectAnInstrumentWithOpenQuantity()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ArchiveAsync(instrument.Id));

        exception.Message.ShouldContain("Vendi");
        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Active);
    }

    [Fact]
    public async Task Archive_ShouldSucceedAfterAFullSale()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 2m, price: 10m);
        await SellAsync(instrument, quantity: 2m, price: 12m);

        var archived = await _service.ArchiveAsync(instrument.Id);

        archived.Status.ShouldBe(InstrumentStatus.Archived);
        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Archived);
    }

    [Fact]
    public async Task RegisterSell_ShouldArchiveTheInstrumentWhenThePositionIsClosed()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 2m, price: 10m);

        await SellAsync(instrument, quantity: 2m, price: 12m);

        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Archived);
    }

    [Fact]
    public async Task RegisterSell_ShouldKeepTheInstrumentActiveWhenQuantityRemains()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 3m, price: 10m);

        await SellAsync(instrument, quantity: 1m, price: 12m);

        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Active);
    }

    [Fact]
    public async Task ImportOperations_ShouldArchiveAnInstrumentSoldOut()
    {
        var instrument = await CreateAsync();
        var buyDate = Today.AddDays(-1);
        var buyRecordedAt =
            new DateTimeOffset(buyDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var sellRecordedAt =
            new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await _service.ImportOperationsAsync(
        [
            new ImportInvestmentOperationRequest(
                Guid.NewGuid(),
                buyRecordedAt,
                new RegisterInvestmentOperationRequest(
                    instrument.Id,
                    InvestmentOperationKind.Buy,
                    buyDate,
                    Quantity: 1m,
                    Price: 10m)),
            new ImportInvestmentOperationRequest(
                Guid.NewGuid(),
                sellRecordedAt,
                new RegisterInvestmentOperationRequest(
                    instrument.Id,
                    InvestmentOperationKind.Sell,
                    Today,
                    Quantity: 1m,
                    Price: 12m)),
        ]);

        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Archived);
    }

    [Fact]
    public async Task RegisterBuy_ShouldCreateTheFeeExpense()
    {
        var instrument = await CreateAsync();

        var buy = await BuyAsync(instrument, quantity: 2m, price: 10m, fee: 2.5m);

        var expense = _operations.Operations.OfType<Expense>().ShouldHaveSingleItem();
        expense.Amount.Value.Amount.ShouldBe(2.5m);
        expense.Category.Name.ShouldBe("Fees");
        expense.Category.Subcategory.ShouldBe("Conto Titoli");
        expense.OperationDate.ShouldBe(buy.OperationDate);
        expense.Description.ShouldBe("Commissioni su acquisto");
    }

    [Fact]
    public async Task UpdateOperation_ShouldUpdateAndRemoveTheFeeExpense()
    {
        var instrument = await CreateAsync();
        var buy = await BuyAsync(instrument, quantity: 2m, price: 10m, fee: 2.5m);
        var feeExpenseId = _operations.Operations.OfType<Expense>().Single().Id;

        await _service.UpdateOperationAsync(new UpdateInvestmentOperationRequest(
            buy.Id,
            Today,
            Quantity: 2m,
            Price: 10m,
            FeeAmount: 3m));

        var updatedExpense = _operations.Operations.OfType<Expense>().ShouldHaveSingleItem();
        updatedExpense.Id.ShouldBe(feeExpenseId);
        updatedExpense.Amount.Value.Amount.ShouldBe(3m);

        await _service.UpdateOperationAsync(new UpdateInvestmentOperationRequest(
            buy.Id,
            Today,
            Quantity: 2m,
            Price: 10m,
            FeeAmount: null));

        _operations.Operations.OfType<Expense>().ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteOperation_ShouldRemoveTheFeeExpense()
    {
        var instrument = await CreateAsync();
        var buy = await BuyAsync(instrument, fee: 2m);

        await _service.DeleteOperationAsync(buy.Id);

        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveIncomeTax_ShouldCreateAndRemoveTheLinkedExpense()
    {
        var income = AddIncome(100m, Today);

        await _service.SaveIncomeTaxAsync(income.Id, 12.5m);

        var tax = _operations.Operations.OfType<Expense>().ShouldHaveSingleItem();
        tax.Amount.Value.Amount.ShouldBe(12.5m);
        tax.Category.Name.ShouldBe("Fees");
        tax.Category.Subcategory.ShouldBe("Tasse Investimenti");
        tax.OperationDate.ShouldBe(income.OperationDate);

        await _service.SaveIncomeTaxAsync(income.Id, null);

        _operations.Operations.OfType<Expense>().ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveIncomeTax_ShouldLinkTheInstrument()
    {
        var instrument = await CreateAsync();
        var income = AddIncome(100m, Today, instrument);

        await _service.SaveIncomeTaxAsync(income.Id, 12.5m);

        var tax = _operations.Operations.OfType<Expense>().ShouldHaveSingleItem();
        tax.Instrument.ShouldBe(instrument);
    }

    [Fact]
    public async Task DeleteIncome_ShouldRemoveTheLinkedTaxExpense()
    {
        var income = AddIncome(100m, Today);
        await _service.SaveIncomeTaxAsync(income.Id, 10m);

        var deletion = new OperationDeletionService(_operations);
        await deletion.DeleteAsync(income.Id);

        _operations.Operations.ShouldBeEmpty();
    }

    [Fact]
    public async Task History_ShouldExposeTheLinkedTaxForIncome()
    {
        var income = AddIncome(100m, Today);
        await _service.SaveIncomeTaxAsync(income.Id, 7m);

        var history = new OperationHistoryService(_operations, _categories);
        var row = (await history.ListAsync()).Single(candidate => candidate.Id == income.Id);

        row.Tax.ShouldNotBeNull().Amount.ShouldBe(7m);
    }

    [Fact]
    public async Task UpdateOperation_ShouldKeepTaxExpensesSeparateForSalesOnTheSameDay()
    {
        var first = await CreateAsync(name: "ETF uno", isin: "IE00B4L5Y983");
        var second = await CreateAsync(name: "ETF due", isin: "IE00B4L5Y984");
        var buyDate = Today.AddDays(-2);
        var sellDate = Today.AddDays(-1);

        await BuyAsync(first, quantity: 1m, price: 10m, date: buyDate);
        var firstSell = await SellAsync(first, quantity: 1m, price: 12m, date: sellDate);
        await BuyAsync(second, quantity: 1m, price: 10m, date: buyDate);
        var secondSell = await SellAsync(second, quantity: 1m, price: 12m, date: sellDate);

        await _service.ReactivateAsync(first.Id);
        await _service.ReactivateAsync(second.Id);

        await _service.UpdateOperationAsync(new UpdateInvestmentOperationRequest(
            firstSell.Id,
            sellDate,
            Quantity: 1m,
            Price: 12m,
            TaxAmount: 8m));
        await _service.UpdateOperationAsync(new UpdateInvestmentOperationRequest(
            secondSell.Id,
            sellDate,
            Quantity: 1m,
            Price: 12m,
            TaxAmount: 3m));

        var taxes = _operations.Operations
            .OfType<Expense>()
            .Where(expense => string.Equals(
                expense.Category.Subcategory,
                "Tasse Investimenti",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(expense => expense.Amount.Value.Amount)
            .ToArray();

        taxes.Length.ShouldBe(2);
        taxes[0].Amount.Value.Amount.ShouldBe(3m);
        taxes[1].Amount.Value.Amount.ShouldBe(8m);
        taxes[0].Id.ShouldNotBe(taxes[1].Id);
    }

    [Fact]
    public async Task ArchiveSoldOut_ShouldArchiveAnInstrumentWithoutHeldQuantity()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 1m, price: 10m);
        await SellAsync(instrument, quantity: 1m, price: 12m);
        await _service.ReactivateAsync(instrument.Id);

        await _service.ArchiveSoldOutAsync([instrument.Id]);

        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Archived);
    }

    [Fact]
    public async Task ArchiveSoldOut_ShouldKeepAnInstrumentWithHeldQuantityActive()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument, quantity: 2m, price: 10m);
        await SellAsync(instrument, quantity: 1m, price: 12m);

        await _service.ArchiveSoldOutAsync([instrument.Id]);

        _instruments.Instruments.ShouldHaveSingleItem().Status.ShouldBe(InstrumentStatus.Active);
    }

    [Fact]
    public async Task Archive_ShouldSucceedForAnInstrumentNeverTraded()
    {
        var instrument = await CreateAsync();

        var archived = await _service.ArchiveAsync(instrument.Id);
        var reactivated = await _service.ReactivateAsync(archived.Id);

        reactivated.Status.ShouldBe(InstrumentStatus.Active);
    }

    [Fact]
    public async Task RecordPrice_ShouldPersistAQuotation()
    {
        var instrument = await CreateAsync();

        var observation = await _service.RecordPriceAsync(
            new RecordPriceRequest(instrument.Id, 10.5m, Today));

        observation.Price.ShouldBe(10.5m);
        observation.ValidAt.ShouldBe(Today);
        observation.Source.ShouldBe("Manuale");
        _prices.Observations.ShouldContain(observation);
    }

    [Fact]
    public async Task RecordPrice_ShouldPersistTheLastTradeTime()
    {
        var instrument = await CreateAsync();

        var observation = await _service.RecordPriceAsync(
            new RecordPriceRequest(
                instrument.Id,
                10.5m,
                Today,
                "Borsa Italiana",
                new TimeOnly(10, 23)));

        observation.TradeTime.ShouldBe(new TimeOnly(10, 23));
    }

    [Fact]
    public async Task RecordPrice_ShouldRejectAFutureDate()
    {
        var instrument = await CreateAsync();

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.RecordPriceAsync(
                new RecordPriceRequest(instrument.Id, 10m, Today.AddDays(1))));

        exception.Message.ShouldContain("successiva");
        _prices.Observations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_ShouldRejectAValuationShapeChangeAfterOperations()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UpdateAsync(new UpdateInvestmentAssetRequest(
                instrument.Id,
                instrument.Name,
                instrument.AssetClass,
                PriceUnit.PercentOfNominal,
                new Money(1000m, Euro))));

        exception.Message.ShouldContain("unità di prezzo");
    }

    [Fact]
    public async Task Update_ShouldPersistDescriptiveChanges()
    {
        var instrument = await CreateAsync();

        var updated = await _service.UpdateAsync(new UpdateInvestmentAssetRequest(
            instrument.Id,
            "ETF aggiornato",
            AssetClass.Mixed,
            PriceUnit.PerUnit,
            Symbol: "NEW",
            Isin: "IE00B4L5Y983",
            Notes: "Nota",
            TaxRate: 12.5m));

        updated.Name.ShouldBe("ETF aggiornato");
        updated.AssetClass.ShouldBe(AssetClass.Mixed);
        updated.Isin.ShouldBe("IE00B4L5Y983");
        updated.Notes.ShouldBe("Nota");
        updated.TaxRate.ShouldBe(12.5m);
        _instruments.Instruments.ShouldHaveSingleItem().Name.ShouldBe("ETF aggiornato");
    }

    [Fact]
    public async Task Delete_ShouldRejectAnInstrumentWithOperations()
    {
        var instrument = await CreateAsync();
        await BuyAsync(instrument);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteAsync(instrument.Id));

        exception.Message.ShouldContain("operazioni");
        _instruments.Instruments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_ShouldRejectAnInstrumentWithQuotations()
    {
        var instrument = await CreateAsync();
        await _service.RecordPriceAsync(new RecordPriceRequest(instrument.Id, 10m, Today));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.DeleteAsync(instrument.Id));

        exception.Message.ShouldContain("quotazioni");
    }

    [Fact]
    public async Task Delete_ShouldRemoveAnInstrumentWithoutHistory()
    {
        var instrument = await CreateAsync();

        await _service.DeleteAsync(instrument.Id);

        _instruments.Instruments.ShouldBeEmpty();
    }

    private Task<Instrument> CreateAsync(
        string name = "ETF globale",
        AssetClass assetClass = AssetClass.Equity,
        PriceUnit priceUnit = PriceUnit.PerUnit,
        Money? notional = null,
        string? symbol = null,
        string? isin = null,
        decimal taxRate = Instrument.DefaultTaxRate) =>
        _service.CreateAsync(new NewInvestmentAsset(
            name,
            assetClass,
            priceUnit,
            notional,
            symbol,
            isin,
            TaxRate: taxRate));

    private Income AddIncome(decimal amount, DateOnly date, Instrument? instrument = null)
    {
        var category = new Category(Guid.NewGuid(), "Cedole", CategoryType.Income);
        var income = new Income(
            Guid.NewGuid(),
            account: null,
            category,
            new PositiveMoney(new Money(amount, Euro)),
            date,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            instrument: instrument);
        _operations.Operations.Add(income);
        return income;
    }

    private Task<InvestmentOperation> BuyAsync(
        Instrument instrument,
        decimal quantity = 1m,
        decimal price = 10m,
        DateOnly? date = null,
        decimal? fee = null) =>
        _service.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            InvestmentOperationKind.Buy,
            date ?? Today,
            quantity,
            price,
            FeeAmount: fee));

    private Task<InvestmentOperation> SellAsync(
        Instrument instrument,
        decimal quantity = 1m,
        decimal price = 10m,
        DateOnly? date = null,
        decimal? fee = null) =>
        _service.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            InvestmentOperationKind.Sell,
            date ?? Today,
            quantity,
            price,
            FeeAmount: fee));
}
