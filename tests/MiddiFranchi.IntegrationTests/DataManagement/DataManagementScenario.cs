using MiddiFranchi.Application.Financial;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Repositories;

namespace MiddiFranchi.IntegrationTests.DataManagement;

internal static class DataManagementScenario
{
    public static async Task SeedAsync(MiddiFranchiDbContext context)
    {
        var accounts = new AccountRepository(context);
        var categories = new CategoryRepository(context);
        var instruments = new InstrumentRepository(context);
        var operations = new FinancialOperationRepository(context);
        var prices = new PriceObservationRepository(context);
        var balances = new ManualBalanceRepository(context);
        var loans = new LoanRepository(context);
        var readings = new LoanReadingRepository(context);
        var metadata = new AppMetadataRepository(context);

        var checking = new Account(
            Guid.NewGuid(),
            "Conto corrente test",
            AccountType.Checking,
            Currency.Euro,
            AccountStatus.Active,
            "#123456",
            null);
        var cash = new Account(
            Guid.NewGuid(),
            "Cassa test",
            AccountType.Cash,
            Currency.Euro,
            AccountStatus.Active);
        var loansAccount = new Account(
            Guid.NewGuid(),
            "Prestiti test",
            AccountType.Loans,
            Currency.Euro,
            AccountStatus.Active);
        await accounts.AddAsync(checking);
        await accounts.AddAsync(cash);
        await accounts.AddAsync(loansAccount);

        var incomeCategory = new Category(
            Guid.NewGuid(),
            "Stipendio test",
            CategoryType.Income,
            icon: "wallet",
            colorHex: "#16a34a");
        var expenseCategory = new Category(
            Guid.NewGuid(),
            "Spesa test",
            CategoryType.Expense,
            subcategory: "Alimentazione",
            icon: "utensils",
            colorHex: "#22c55e");
        await categories.AddAsync(incomeCategory);
        await categories.AddAsync(expenseCategory);

        var instrument = new Instrument(
            Guid.NewGuid(),
            "ETF test",
            AssetClass.Equity,
            Currency.Euro,
            PriceUnit.PerUnit,
            symbol: "VWCE",
            isin: "IE00BK5BQT80");
        await instruments.AddAsync(instrument);

        var registrations = new OperationRegistrationService(
            new AccountRepository(context),
            new CategoryRepository(context),
            new FinancialOperationRepository(context));
        var date = new DateOnly(2026, 1, 10);

        await registrations.IncomeAsync(new RegisterIncomeRequest(
            incomeCategory.Id,
            new PositiveMoney(new Money(1000m, Currency.Euro)),
            date));
        await registrations.ExpenseAsync(new RegisterExpenseRequest(
            expenseCategory.Id,
            new PositiveMoney(new Money(100m, Currency.Euro)),
            date.AddDays(1)));
        await registrations.TransferAsync(new RegisterTransferRequest(
            checking.Id,
            cash.Id,
            new PositiveMoney(new Money(200m, Currency.Euro)),
            new PositiveMoney(new Money(200m, Currency.Euro)),
            date.AddDays(2)));

        var assets = new InvestmentAssetService(
            new InstrumentRepository(context),
            new FinancialOperationRepository(context),
            new PriceObservationRepository(context),
            new CategoryRepository(context),
            new EfTransactionRunner(context));
        await assets.RegisterOperationAsync(new RegisterInvestmentOperationRequest(
            instrument.Id,
            InvestmentOperationKind.Buy,
            date.AddDays(3),
            Quantity: 10m,
            Price: 50m,
            FeeAmount: 2.5m));

        await prices.AddAsync(PriceObservation.AtUnitPrice(
            Guid.NewGuid(),
            instrument,
            new UnitPrice(new Money(52m, Currency.Euro)),
            date.AddDays(4),
            new DateTimeOffset(2026, 1, 14, 10, 0, 0, TimeSpan.Zero),
            "Test"));

        await balances.AddAsync(new ManualBalance(
            Guid.NewGuid(),
            checking,
            new Money(2500m, Currency.Euro),
            date,
            new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero)));

        var loan = new Loan(
            Guid.NewGuid(),
            loansAccount,
            "Mario Rossi",
            new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero));
        await loans.AddAsync(loan);
        await readings.AddAsync(new LoanReading(
            Guid.NewGuid(),
            loan,
            new Money(150m, Currency.Euro),
            date.AddDays(5)));

        await metadata.SetAsync("test.key", "test-value");
    }
}
