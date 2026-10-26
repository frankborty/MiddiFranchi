using System.IO.Compression;
using System.Text;
using MiddiFranchi.Application.DataImport;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.Financial;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Repositories;
using MiddiFranchi.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace MiddiFranchi.IntegrationTests.DataImport;

[Collection(PostgresCollection.Name)]
public class DataImportTests : PostgresTestBase
{
    public DataImportTests(PostgresDatabaseFixture fixture)
        : base(fixture)
    {
    }

    [PostgresFact]
    public async Task Import_ShouldCreateAndUpdateAllSections()
    {
        var accounts = new AccountRepository(Context);
        var balances = new ManualBalanceRepository(Context);
        var instruments = new InstrumentRepository(Context);
        var prices = new PriceObservationRepository(Context);
        var operations = new FinancialOperationRepository(Context);
        var service = BuildService(accounts, balances, instruments, prices, operations);

        var isin = RandomIsin();
        var symbol = RandomSymbol();
        using var archive = BuildArchive(isin, symbol);

        var preview = await service.InspectAsync(archive);
        preview.HasErrors.ShouldBeFalse(string.Join(" | ", preview.Errors));
        preview.Accounts.ShouldBe(1);
        preview.AccountBalances.ShouldBe(1);
        preview.Instruments.ShouldBe(1);
        preview.InstrumentPrices.ShouldBe(1);
        preview.Operations.ShouldBe(1);

        archive.Position = 0;
        var first = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);

        first.AccountsCreated.ShouldBe(1);
        first.BalancesCreated.ShouldBe(1);
        first.InstrumentsCreated.ShouldBe(1);
        first.PricesCreated.ShouldBe(1);
        first.OperationsImported.ShouldBe(1);
        ClearTracker();

        var account = await accounts.GetByIdAsync(
            ImportIdMapper.ToGuid(ImportIdMapper.AccountEntity, "Conto import"));
        account.ShouldNotBeNull();
        account.Name.ShouldBe("Conto import");
        account.Type.ShouldBe(AccountType.Checking);
        account.IconDataUrl.ShouldBe("icon:wallet");

        var balance = await balances.GetByIdAsync(
            ImportIdMapper.ToAccountBalanceGuid("Conto import", new DateOnly(2024, 1, 1)));
        balance.ShouldNotBeNull();
        balance.Amount.Amount.ShouldBe(1000m);
        balance.ValidAt.ShouldBe(new DateOnly(2024, 1, 1));

        var instrument = await instruments.GetByIdAsync(
            ImportIdMapper.ToInstrumentGuidByIsin(isin));
        instrument.ShouldNotBeNull();
        instrument.Name.ShouldBe("ETF import");
        instrument.Isin.ShouldBe(isin);
        instrument.PriceUnit.ShouldBe(Domain.Portfolio.PriceUnit.PerUnit);

        var observations = await prices.ListForInstrumentAsync(instrument.Id, DateOnly.MaxValue);
        observations.ShouldHaveSingleItem().Price.ShouldBe(100.50m);

        var operation = (await operations.ListAsync())
            .OfType<InvestmentOperation>()
            .Single(candidate => candidate.Instrument?.Id == instrument.Id);
        var investment = operation;
        investment.InvestmentKind.ShouldBe(InvestmentOperationKind.Buy);
        investment.Quantity!.Value.Value.ShouldBe(10m);
        investment.QuotedPrice!.Value.PricePerUnit!.Value.Price.Amount.ShouldBe(100.50m);
        investment.Fees.ShouldHaveSingleItem().Amount.Value.Amount.ShouldBe(2.50m);
        ClearTracker();

        archive.Position = 0;
        var second = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);

        second.AccountsCreated.ShouldBe(0);
        second.AccountsUpdated.ShouldBe(1);
        second.BalancesCreated.ShouldBe(0);
        second.BalancesUpdated.ShouldBe(1);
        second.InstrumentsCreated.ShouldBe(0);
        second.InstrumentsUpdated.ShouldBe(1);
        second.PricesCreated.ShouldBe(0);
        second.PricesUpdated.ShouldBe(1);
        second.OperationsImported.ShouldBe(0);
        second.OperationsSkipped.ShouldBe(1);
    }

    [PostgresFact]
    public async Task Import_ShouldAssociateBalancesByAccountNameAndDate()
    {
        var accounts = new AccountRepository(Context);
        var balances = new ManualBalanceRepository(Context);
        var service = BuildService(
            accounts,
            balances,
            new InstrumentRepository(Context),
            new PriceObservationRepository(Context),
            new FinancialOperationRepository(Context));

        var accountName = "Conto senza id " + Guid.NewGuid().ToString("N")[..8];
        using var archive = BuildBalanceArchive(accountName);

        var preview = await service.InspectAsync(archive);
        preview.HasErrors.ShouldBeFalse(string.Join(" | ", preview.Errors));
        preview.Accounts.ShouldBe(1);
        preview.AccountBalances.ShouldBe(2);

        archive.Position = 0;
        var first = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        first.AccountsCreated.ShouldBe(1);
        first.BalancesCreated.ShouldBe(2);
        first.BalancesUpdated.ShouldBe(0);
        ClearTracker();

        var account = (await accounts.ListAsync())
            .Single(candidate => candidate.Name == accountName);
        var readings = await balances.ListForAccountAsync(account.Id);
        readings.Count.ShouldBe(2);
        readings.Single(reading => reading.ValidAt == new DateOnly(2024, 1, 1))
            .Amount.Amount.ShouldBe(1000m);

        archive.Position = 0;
        var second = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        second.BalancesCreated.ShouldBe(0);
        second.BalancesUpdated.ShouldBe(2);
        ClearTracker();

        (await balances.ListForAccountAsync(account.Id)).Count.ShouldBe(2);
    }

    [PostgresFact]
    public async Task Import_ShouldUseIsinAsInstrumentKey()
    {
        var instruments = new InstrumentRepository(Context);
        var prices = new PriceObservationRepository(Context);
        var operations = new FinancialOperationRepository(Context);
        var service = BuildService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            instruments,
            prices,
            operations);

        var isin = RandomIsin();
        using var archive = BuildIsinArchive(isin);

        var preview = await service.InspectAsync(archive);
        preview.HasErrors.ShouldBeFalse(string.Join(" | ", preview.Errors));
        preview.Instruments.ShouldBe(1);
        preview.InstrumentPrices.ShouldBe(1);
        preview.Operations.ShouldBe(1);

        archive.Position = 0;
        var first = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        first.InstrumentsCreated.ShouldBe(1);
        first.PricesCreated.ShouldBe(1);
        first.OperationsImported.ShouldBe(1);
        ClearTracker();

        var instrument = await instruments.GetByIdAsync(
            ImportIdMapper.ToInstrumentGuidByIsin(isin));
        instrument.ShouldNotBeNull();
        instrument.Isin.ShouldBe(isin);
        instrument.Name.ShouldBe("ETF ISIN");

        archive.Position = 0;
        var second = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        second.InstrumentsCreated.ShouldBe(0);
        second.InstrumentsUpdated.ShouldBe(1);
        second.PricesCreated.ShouldBe(0);
        second.PricesUpdated.ShouldBe(1);
        second.OperationsImported.ShouldBe(0);
        second.OperationsSkipped.ShouldBe(1);
        ClearTracker();

        (await instruments.ListAsync()).Count(candidate => candidate.Isin == isin).ShouldBe(1);
    }

    [PostgresFact]
    public async Task Import_ShouldResolveInstrumentBySymbolAndName()
    {
        var instruments = new InstrumentRepository(Context);
        var prices = new PriceObservationRepository(Context);
        var operations = new FinancialOperationRepository(Context);
        var service = BuildService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            instruments,
            prices,
            operations);

        var isin = RandomIsin();
        var symbol = RandomSymbol();
        using var archive = BuildSymbolAndNameArchive(isin, symbol);

        var preview = await service.InspectAsync(archive);
        preview.HasErrors.ShouldBeFalse(string.Join(" | ", preview.Errors));
        preview.Instruments.ShouldBe(1);
        preview.InstrumentPrices.ShouldBe(2);
        preview.Operations.ShouldBe(1);

        archive.Position = 0;
        var first = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        first.PricesCreated.ShouldBe(2);
        first.OperationsImported.ShouldBe(1);
        ClearTracker();

        var instrument = (await instruments.ListAsync()).Single(candidate => candidate.Isin == isin);
        var observations = await prices.ListForInstrumentAsync(instrument.Id, DateOnly.MaxValue);
        observations.Count.ShouldBe(2);

        archive.Position = 0;
        var second = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        second.PricesCreated.ShouldBe(0);
        second.PricesUpdated.ShouldBe(2);
        second.OperationsImported.ShouldBe(0);
        second.OperationsSkipped.ShouldBe(1);
        ClearTracker();
    }

    [PostgresFact]
    public async Task Reimport_ShouldArchiveInstrumentsSoldOutEvenWhenOperationsAreSkipped()
    {
        var instruments = new InstrumentRepository(Context);
        var operations = new FinancialOperationRepository(Context);
        var service = BuildService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            instruments,
            new PriceObservationRepository(Context),
            operations);

        var isin = RandomIsin();
        using var archive = BuildSoldOutArchive(isin);

        var first = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        first.InstrumentsCreated.ShouldBe(1);
        first.OperationsImported.ShouldBe(2);
        ClearTracker();

        var imported = (await instruments.ListAsync()).Single(candidate => candidate.Isin == isin);
        imported.IsArchived.ShouldBeTrue();

        var assets = new InvestmentAssetService(
            instruments,
            operations,
            new PriceObservationRepository(Context),
            new CategoryRepository(Context),
            new EfTransactionRunner(Context));
        await assets.ReactivateAsync(imported.Id);
        ClearTracker();

        archive.Position = 0;
        var second = await service.ImportAsync(archive, ImportContract.ConfirmationPhrase);
        second.OperationsImported.ShouldBe(0);
        second.OperationsSkipped.ShouldBe(2);
        ClearTracker();

        (await instruments.ListAsync())
            .Single(candidate => candidate.Isin == isin)
            .IsArchived.ShouldBeTrue();
    }

    [PostgresFact]
    public async Task Inspect_ShouldReportMissingReferences()
    {
        var accounts = new AccountRepository(Context);
        var balances = new ManualBalanceRepository(Context);
        var instruments = new InstrumentRepository(Context);
        var prices = new PriceObservationRepository(Context);
        var operations = new FinancialOperationRepository(Context);
        var service = BuildService(accounts, balances, instruments, prices, operations);

        using var archive = BuildArchiveWithMissingReferences();

        var preview = await service.InspectAsync(archive);

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("conto-sconosciuto"));
        preview.Errors.ShouldContain(error => error.Contains("etf-sconosciuto"));
    }

    [PostgresFact]
    public async Task Import_ShouldRejectWrongConfirmation()
    {
        var service = BuildService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            new InstrumentRepository(Context),
            new PriceObservationRepository(Context),
            new FinancialOperationRepository(Context));

        using var archive = BuildArchive(RandomIsin(), RandomSymbol());

        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.ImportAsync(archive, "NO"));
    }

    [PostgresFact]
    public async Task Inspect_ShouldValidateDownloadedTemplate()
    {
        var service = BuildService(
            new AccountRepository(Context),
            new ManualBalanceRepository(Context),
            new InstrumentRepository(Context),
            new PriceObservationRepository(Context),
            new FinancialOperationRepository(Context));

        var template = await service.CreateTemplateAsync();
        using var archive = new MemoryStream(template.Content);
        var preview = await service.InspectAsync(archive);

        preview.HasErrors.ShouldBeFalse(string.Join(" | ", preview.Errors));
        preview.Accounts.ShouldBe(2);
        preview.Instruments.ShouldBe(1);
        preview.Operations.ShouldBe(1);
    }

    private DataImportService BuildService(
        AccountRepository accounts,
        ManualBalanceRepository balances,
        InstrumentRepository instruments,
        PriceObservationRepository prices,
        FinancialOperationRepository operations) =>
        new(
            accounts,
            balances,
            instruments,
            prices,
            operations,
            new LoanRepository(Context),
            new InvestmentAssetService(
                instruments,
                operations,
                prices,
                new CategoryRepository(Context),
                new EfTransactionRunner(Context)),
            new EfTransactionRunner(Context),
            new DataMaintenanceGate());

    private static string RandomIsin() =>
        "IT" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static string RandomSymbol() =>
        "IMP" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static MemoryStream BuildArchive(
        string isin,
        string symbol,
        string accountName = "Conto import")
    {
        var accounts = CsvDocument.Write(
            ["name", "type", "status", "color_hex", "icon", "loans"],
            [
                new string?[] { accountName, "Checking", "Active", "#2563eb", "wallet", "" },
            ]);

        var balances = CsvDocument.Write(
            ["account_name", "date", "amount"],
            [
                new string?[] { accountName, "2024-01-01", "1000.00" },
            ]);

        var instruments = CsvDocument.Write(
            [
                "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate",
            ],
            [
                new string?[]
                {
                    "ETF import", "Equity", "Active", symbol, isin,
                    "EUR", "PerUnit", "", "", "", "", "", "",
                },
            ]);

        var prices = CsvDocument.Write(
            ["instrument_isin", "date", "price", "currency", "source"],
            [
                new string?[] { isin, "2024-01-02", "100.50", "EUR", "Import" },
            ]);

        var operations = CsvDocument.Write(
            [
                "kind", "date", "instrument_isin",
                "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "Buy", "2024-01-03", isin, "10", "100.50", "2.50", "Acquisto",
                },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Accounts, accounts);
            WriteEntry(archive, ImportFileNames.AccountBalances, balances);
            WriteEntry(archive, ImportFileNames.Instruments, instruments);
            WriteEntry(archive, ImportFileNames.InstrumentPrices, prices);
            WriteEntry(archive, ImportFileNames.Operations, operations);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildIsinArchive(string isin)
    {
        var instruments = CsvDocument.Write(
            ["name", "asset_class", "price_unit", "isin"],
            [
                new string?[] { "ETF ISIN", "Equity", "PerUnit", isin },
            ]);

        var prices = CsvDocument.Write(
            ["instrument_isin", "date", "price", "currency", "source"],
            [
                new string?[] { isin, "2024-01-02", "100.50", "EUR", "Import" },
            ]);

        var operations = CsvDocument.Write(
            ["kind", "date", "instrument_isin", "quantity", "price", "fee", "description"],
            [
                new string?[]
                {
                    "Buy", "2024-01-03", isin, "10", "100.50", "2.50", "Acquisto",
                },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Instruments, instruments);
            WriteEntry(archive, ImportFileNames.InstrumentPrices, prices);
            WriteEntry(archive, ImportFileNames.Operations, operations);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildSoldOutArchive(string isin)
    {
        var instruments = CsvDocument.Write(
            ["name", "asset_class", "price_unit", "isin"],
            [
                new string?[] { "ETF venduto", "Equity", "PerUnit", isin },
            ]);

        var operations = CsvDocument.Write(
            ["kind", "date", "instrument_isin", "quantity", "price"],
            [
                new string?[] { "Buy", "2024-01-02", isin, "2", "10" },
                new string?[] { "Sell", "2024-01-03", isin, "2", "12" },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Instruments, instruments);
            WriteEntry(archive, ImportFileNames.Operations, operations);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildSymbolAndNameArchive(string isin, string symbol)
    {
        var instruments = CsvDocument.Write(
            ["name", "asset_class", "price_unit", "isin", "symbol"],
            [
                new string?[] { "ETF per nome", "Equity", "PerUnit", isin, symbol },
            ]);

        var prices = CsvDocument.Write(
            ["instrument_symbol", "instrument_name", "date", "price", "currency", "source"],
            [
                new string?[] { symbol, "", "2024-01-02", "100.50", "EUR", "Import" },
                new string?[] { "", "ETF per nome", "2024-01-03", "101.50", "EUR", "Import" },
            ]);

        var operations = CsvDocument.Write(
            [
                "kind", "date", "instrument_name",
                "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "Buy", "2024-01-04", "ETF per nome", "10", "101.50", "0", "",
                },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Instruments, instruments);
            WriteEntry(archive, ImportFileNames.InstrumentPrices, prices);
            WriteEntry(archive, ImportFileNames.Operations, operations);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildBalanceArchive(string accountName)
    {
        var accounts = CsvDocument.Write(
            ["name", "type", "status", "color_hex", "icon", "loans"],
            [
                new string?[] { accountName, "Checking", "Active", "", "", "" },
            ]);

        var balances = CsvDocument.Write(
            ["account_name", "date", "amount"],
            [
                new string?[] { accountName, "2024-01-01", "1000.00" },
                new string?[] { accountName, "2024-02-01", "1250.50" },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Accounts, accounts);
            WriteEntry(archive, ImportFileNames.AccountBalances, balances);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream BuildArchiveWithMissingReferences()
    {
        var accounts = CsvDocument.Write(
            ["name", "type", "status", "color_hex", "icon", "loans"],
            []);
        var balances = CsvDocument.Write(
            ["account_name", "date", "amount"],
            [
                new string?[] { "conto-sconosciuto", "2024-01-01", "1000.00" },
            ]);
        var instruments = CsvDocument.Write(
            [
                "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate",
            ],
            []);
        var prices = CsvDocument.Write(
            ["instrument_name", "date", "price", "currency", "source"],
            [
                new string?[] { "ETF sconosciuto", "2024-01-02", "10", "EUR", "Import" },
            ]);
        var operations = CsvDocument.Write(
            [
                "kind", "date", "instrument_name",
                "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "Buy", "2024-01-03", "etf-sconosciuto", "1", "10", "0", "",
                },
            ]);

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ImportFileNames.Accounts, accounts);
            WriteEntry(archive, ImportFileNames.AccountBalances, balances);
            WriteEntry(archive, ImportFileNames.Instruments, instruments);
            WriteEntry(archive, ImportFileNames.InstrumentPrices, prices);
            WriteEntry(archive, ImportFileNames.Operations, operations);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);
    }
}
