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
    private readonly FakeFinancialOperationRepository _operations = new();
    private readonly FakePriceObservationRepository _prices = new();
    private readonly InvestmentAssetService _service;
    private readonly Account _account;

    public InvestmentAssetServiceTests()
    {
        _account = new Account(
            Guid.NewGuid(), "Conto titoli", AccountType.Checking, Euro, AccountStatus.Active);
        _accounts.Accounts.Add(_account);
        _service = new InvestmentAssetService(
            _instruments,
            _accounts,
            _operations,
            _prices,
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
        _instruments.Instruments.ShouldHaveSingleItem().ShouldBe(created);
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
        buy.SettlementAccount.Id.ShouldBe(_account.Id);
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
            _account.Id,
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
            _account.Id,
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
            Notes: "Nota"));

        updated.Name.ShouldBe("ETF aggiornato");
        updated.AssetClass.ShouldBe(AssetClass.Mixed);
        updated.Isin.ShouldBe("IE00B4L5Y983");
        updated.Notes.ShouldBe("Nota");
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
        string? isin = null) =>
        _service.CreateAsync(new NewInvestmentAsset(
            name,
            assetClass,
            priceUnit,
            notional,
            symbol,
            isin));

    private Task<InvestmentOperation> BuyAsync(
        Instrument instrument,
        decimal quantity = 1m,
        decimal price = 10m,
        DateOnly? date = null) =>
        _service.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            _account.Id,
            InvestmentOperationKind.Buy,
            date ?? Today,
            quantity,
            price));

    private Task<InvestmentOperation> SellAsync(
        Instrument instrument,
        decimal quantity = 1m,
        decimal price = 10m,
        DateOnly? date = null) =>
        _service.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            _account.Id,
            InvestmentOperationKind.Sell,
            date ?? Today,
            quantity,
            price));
}
