using System.Text;
using MiddiFranchi.Application.DataImport;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public class AccountsImportServiceTests
{
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeLoanRepository _loans = new();

    private readonly AccountsImportService _service;

    public AccountsImportServiceTests()
    {
        _service = new AccountsImportService(
            _accounts,
            _loans,
            new FakeTransactionRunner(),
            new DataMaintenanceGate());
    }

    [Fact]
    public async Task Import_ShouldCreateAccountsWithSettings()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Directa,Checking,Active,,wallet
                PostePay,Card,Closed,#2563EB,"data:image/png;base64,AAA"
                """));

        preview.Errors.ShouldBeEmpty();
        preview.Count.ShouldBe(2);

        var result = await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Directa,Checking,Active,,wallet
                PostePay,Card,Closed,#2563EB,"data:image/png;base64,AAA"
                """),
            ImportContract.ConfirmationPhrase);

        result.AccountsCreated.ShouldBe(2);
        result.AccountsUpdated.ShouldBe(0);
        _accounts.Accounts.Count.ShouldBe(2);

        var directa = _accounts.Accounts.Single(account => account.Name == "Directa");
        directa.Type.ShouldBe(AccountType.Checking);
        directa.Status.ShouldBe(AccountStatus.Active);
        directa.Currency.Code.ShouldBe(Currency.Euro.Code);
        directa.ColorHex.ShouldBeNull();
        directa.IconDataUrl.ShouldBe("icon:wallet");

        var postepay = _accounts.Accounts.Single(account => account.Name == "PostePay");
        postepay.Type.ShouldBe(AccountType.Card);
        postepay.Status.ShouldBe(AccountStatus.Closed);
        postepay.ColorHex.ShouldBe("#2563eb");
        postepay.IconDataUrl.ShouldStartWith("data:image/");
    }

    [Fact]
    public async Task Import_ShouldUpdateExistingAccountsByNameIgnoringCase()
    {
        await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Carta commerciale,Card,Active,#2563eb,wallet
                """),
            ImportContract.ConfirmationPhrase);
        var existing = _accounts.Accounts.ShouldHaveSingleItem();

        var second = await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                carta commerciale,Card,Closed,,icon:credit-card
                """),
            ImportContract.ConfirmationPhrase);

        second.AccountsCreated.ShouldBe(0);
        second.AccountsUpdated.ShouldBe(1);
        _accounts.Accounts.Count.ShouldBe(1);
        _accounts.Accounts[0].Id.ShouldBe(existing.Id);
        _accounts.Accounts[0].Name.ShouldBe(existing.Name);
        _accounts.Accounts[0].Status.ShouldBe(AccountStatus.Closed);
        _accounts.Accounts[0].ColorHex.ShouldBeNull();
        _accounts.Accounts[0].IconDataUrl.ShouldBe("icon:credit-card");
    }

    [Fact]
    public async Task Import_ShouldRejectDuplicateNamesInTheFile()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Euro,Cash,Active,,
                euro,Cash,Active,,
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("nome conto duplicato"));
    }

    [Fact]
    public async Task Import_ShouldRejectAnUnknownType()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Directa,Savings,Active,,
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("type"));

        await Should.ThrowAsync<ImportValidationException>(
            () => _service.ImportAsync(
                StreamOf(
                    """
                    name,type,status,color_hex,icon
                    Directa,Savings,Active,,
                    """),
                ImportContract.ConfirmationPhrase));

        _accounts.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Inspect_ShouldRejectAMissingHeader()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                nome,identificativo
                Directa,1
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("Intestazioni mancanti"));
    }

    [Fact]
    public async Task Import_ShouldRequireTheConfirmationPhrase()
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.ImportAsync(
                StreamOf(ValidCsv()),
                "SICURO"));

        exception.Message.ShouldContain("IMPORTA");
        _accounts.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_ShouldRejectChangingTypeOfALoansAccountWithLoans()
    {
        await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Prestito Aldo,Loans,Active,,
                """),
            ImportContract.ConfirmationPhrase);
        var account = _accounts.Accounts.ShouldHaveSingleItem();
        _loans.Loans.Add(new Loan(Guid.NewGuid(), account, "Aldo", DateTimeOffset.UtcNow));

        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Prestito Aldo,Checking,Active,,
                """));
        preview.Errors.ShouldContain(error => error.Contains("ha prestiti registrati"));

        await Should.ThrowAsync<ImportValidationException>(
            () => _service.ImportAsync(
                StreamOf(
                    """
                    name,type,status,color_hex,icon
                    Prestito Aldo,Checking,Active,,
                    """),
                ImportContract.ConfirmationPhrase));

        _accounts.Accounts.Count.ShouldBe(1);
        _accounts.Accounts[0].Type.ShouldBe(AccountType.Loans);
    }

    [Fact]
    public async Task Import_ShouldAllowChangingTypeOfALoansAccountWithoutLoans()
    {
        await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Prestiti vecchi,Loans,Active,,
                """),
            ImportContract.ConfirmationPhrase);

        var result = await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Prestiti vecchi,Checking,Active,,
                """),
            ImportContract.ConfirmationPhrase);

        result.AccountsUpdated.ShouldBe(1);
        _accounts.Accounts[0].Type.ShouldBe(AccountType.Checking);
    }

    [Fact]
    public async Task Import_ShouldRejectAnInvalidColor()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon
                Directa,Checking,Active,#blu,
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("#RRGGBB"));
    }

    [Fact]
    public async Task Import_ShouldCreateLoanNamesOnlyAndBeIdempotent()
    {
        var csv =
            """
            name,type,status,color_hex,icon,loans
            Prestito Famiglia,Loans,Active,,,"Martina; Virginia"
            """;

        var first = await _service.ImportAsync(StreamOf(csv), ImportContract.ConfirmationPhrase);

        first.LoansCreated.ShouldBe(2);
        var account = _accounts.Accounts.ShouldHaveSingleItem();
        account.Type.ShouldBe(AccountType.Loans);
        _loans.Loans
            .Select(loan => loan.Borrower)
            .ShouldBe(["Martina", "Virginia"], ignoreOrder: true);
        _loans.Loans.ShouldAllBe(loan => loan.AccountId == account.Id);

        var second = await _service.ImportAsync(StreamOf(csv), ImportContract.ConfirmationPhrase);

        second.LoansCreated.ShouldBe(0);
        _loans.Loans.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Import_ShouldOnlyCreateLoanNamesNotAlreadyPresent()
    {
        var csv =
            """
            name,type,status,color_hex,icon,loans
            Prestito Famiglia,Loans,Active,,,"Martina; Virginia"
            """;
        await _service.ImportAsync(StreamOf(csv), ImportContract.ConfirmationPhrase);

        var result = await _service.ImportAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon,loans
                Prestito Famiglia,Loans,Active,,,"Martina; Aldo"
                """),
            ImportContract.ConfirmationPhrase);

        result.LoansCreated.ShouldBe(1);
        _loans.Loans.Count.ShouldBe(3);
        _loans.Loans.ShouldContain(loan => loan.Borrower == "Aldo");
    }

    [Fact]
    public async Task Import_ShouldRejectLoansOnANonLoansAccount()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon,loans
                Carta,Card,Active,,,"Prestito X"
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("solo su conti di tipo Loans"));
    }

    [Fact]
    public async Task Import_ShouldRejectADuplicateLoanNameInTheSameRow()
    {
        var preview = await _service.InspectAsync(
            StreamOf(
                """
                name,type,status,color_hex,icon,loans
                Prestiti,Loans,Active,,,"Aldo; aldo"
                """));

        preview.HasErrors.ShouldBeTrue();
        preview.Errors.ShouldContain(error => error.Contains("nome di prestito duplicato"));
    }

    [Fact]
    public async Task Template_ShouldContainTheHeaderAndAnExample()
    {
        var template = await _service.CreateTemplateAsync();

        template.FileName.ShouldBe("MiddiFranchi-import-conti.csv");
        Encoding.UTF8.GetString(template.Content)
            .ShouldContain("name,type,status,color_hex,icon");
        Encoding.UTF8.GetString(template.Content)
            .ShouldContain("Conto esempio");
    }

    private static string ValidCsv() =>
        """
        name,type,status,color_hex,icon
        Euro,Cash,Active,,
        """;

    private static Stream StreamOf(string csv)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return new MemoryStream(bytes);
    }
}
