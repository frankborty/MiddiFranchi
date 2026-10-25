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
            ExternalIdMapper.ToGuid(ExternalIdMapper.AccountEntity, "conto-import"));
        account.ShouldNotBeNull();
        account.Name.ShouldBe("Conto import");
        account.Type.ShouldBe(AccountType.Checking);
        account.IconDataUrl.ShouldBe("icon:wallet");

        var balance = await balances.GetByIdAsync(
            ExternalIdMapper.ToGuid(ExternalIdMapper.AccountBalanceEntity, "saldo-import"));
        balance.ShouldNotBeNull();
        balance.Amount.Amount.ShouldBe(1000m);
        balance.ValidAt.ShouldBe(new DateOnly(2024, 1, 1));

        var instrument = await instruments.GetByIdAsync(
            ExternalIdMapper.ToGuid(ExternalIdMapper.InstrumentEntity, "etf-import"));
        instrument.ShouldNotBeNull();
        instrument.Name.ShouldBe("ETF import");
        instrument.Isin.ShouldBe(isin);
        instrument.PriceUnit.ShouldBe(Domain.Portfolio.PriceUnit.PerUnit);

        var observations = await prices.ListForInstrumentAsync(instrument.Id, DateOnly.MaxValue);
        observations.ShouldHaveSingleItem().Price.ShouldBe(100.50m);

        var operation = await operations.GetByIdAsync(
            ExternalIdMapper.ToGuid(ExternalIdMapper.OperationEntity, "operazione-import"));
        var investment = operation.ShouldBeOfType<InvestmentOperation>();
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
        preview.Accounts.ShouldBe(1);
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
            new InvestmentAssetService(instruments, accounts, operations, prices),
            new EfTransactionRunner(Context),
            new DataMaintenanceGate());

    private static string RandomIsin() =>
        "IT" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private static string RandomSymbol() =>
        "IMP" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private static MemoryStream BuildArchive(
        string isin,
        string symbol,
        string accountExternalId = "conto-import",
        string instrumentExternalId = "etf-import")
    {
        var accounts = CsvDocument.Write(
            ["external_id", "name", "type", "status", "color_hex", "icon"],
            [
                new string?[]
                {
                    accountExternalId, "Conto import", "Checking", "Active", "#2563eb", "wallet",
                },
            ]);

        var balances = CsvDocument.Write(
            ["external_id", "account_external_id", "date", "amount"],
            [
                new string?[]
                {
                    "saldo-import", accountExternalId, "2024-01-01", "1000.00",
                },
            ]);

        var instruments = CsvDocument.Write(
            [
                "external_id", "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate",
            ],
            [
                new string?[]
                {
                    instrumentExternalId, "ETF import", "Equity", "Active", symbol, isin,
                    "EUR", "PerUnit", "", "", "", "", "", "",
                },
            ]);

        var prices = CsvDocument.Write(
            ["external_id", "instrument_external_id", "date", "price", "currency", "source"],
            [
                new string?[]
                {
                    "prezzo-import", instrumentExternalId, "2024-01-02", "100.50", "EUR", "Import",
                },
            ]);

        var operations = CsvDocument.Write(
            [
                "external_id", "kind", "date", "instrument_external_id",
                "settlement_account_external_id", "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "operazione-import", "Buy", "2024-01-03", instrumentExternalId,
                    accountExternalId, "10", "100.50", "2.50", "Acquisto",
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

    private static MemoryStream BuildArchiveWithMissingReferences()
    {
        var accounts = CsvDocument.Write(
            ["external_id", "name", "type", "status", "color_hex", "icon"],
            []);
        var balances = CsvDocument.Write(
            ["external_id", "account_external_id", "date", "amount"],
            []);
        var instruments = CsvDocument.Write(
            [
                "external_id", "name", "asset_class", "status", "symbol", "isin",
                "quote_currency", "price_unit", "notional_value", "notional_currency",
                "reference_url", "notes", "maturity_date", "coupon_rate",
            ],
            []);
        var prices = CsvDocument.Write(
            ["external_id", "instrument_external_id", "date", "price", "currency", "source"],
            []);
        var operations = CsvDocument.Write(
            [
                "external_id", "kind", "date", "instrument_external_id",
                "settlement_account_external_id", "quantity", "price", "fee", "description",
            ],
            [
                new string?[]
                {
                    "operazione-mancante", "Buy", "2024-01-03", "etf-sconosciuto",
                    "conto-sconosciuto", "1", "10", "0", "",
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
