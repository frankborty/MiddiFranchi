using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class TransferTests
{
    private static readonly Currency Euro = Currency.Parse("EUR");
    private static readonly Currency Usd = Currency.Parse("USD");
    private static readonly Currency Gbp = Currency.Parse("GBP");
    private static readonly ExchangeRate UsdRate = new(Usd, 1.08m);
    private static readonly Guid OperationId = Guid.Parse("8a92bb63-c3d4-4a01-9f9f-4c8a0d3f1e2d");
    private static readonly DateOnly OperationDate = new(2026, 3, 12);
    private static readonly DateTimeOffset RecordedAt = new(2026, 3, 12, 15, 0, 0, TimeSpan.Zero);
    private static readonly Account CheckingAccount = new(
        Guid.Parse("d2f104e2-26bd-46dc-9db3-b83c059fe0a4"), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account SavingsAccount = new(
        Guid.Parse("6c7f1e09-8f2d-4c6b-bd0e-c8a4c8402f9c"), "Risparmio", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account UsdAccount = new(
        Guid.Parse("9ae1be54-3eef-4971-9ad4-d58ca44dd7cb"), "Conto USD", AccountType.Checking, Usd, AccountStatus.Active);
    private static readonly Account GbpAccount = new(
        Guid.Parse("2ff8522f-e1b9-4feb-a0a0-a3cff9603cbf"), "Conto GBP", AccountType.Checking, Gbp, AccountStatus.Active);

    [Fact]
    public void Transfer_ShouldExposeTheTransferKind()
    {
        SameCurrencyTransfer(300m).Kind.ShouldBe(OperationKind.Transfer);
    }

    [Fact]
    public void Generation_ShouldProduceTwoLinkedPostingsInTheTransferLegKind()
    {
        var transfer = SameCurrencyTransfer(300m);

        var postings = transfer.GeneratePostings();

        postings.Count.ShouldBe(2);
        postings.ShouldAllBe(posting => posting.Kind == PostingKind.TransferLeg);
        postings.ShouldAllBe(posting => posting.OperationId == OperationId);
        postings.ShouldAllBe(posting => posting.OperationDate == OperationDate);
    }

    [Fact]
    public void Generation_ShouldApplyTheNegativeSignToTheSourceAndThePositiveSignToTheDestination()
    {
        var transfer = SameCurrencyTransfer(300m);

        var postings = transfer.GeneratePostings();
        var source = postings.Single(posting => posting.AccountId == CheckingAccount.Id);
        var destination = postings.Single(posting => posting.AccountId == SavingsAccount.Id);

        source.SignedAmount.ShouldBe(-300m);
        source.Currency.ShouldBe(Euro);
        destination.SignedAmount.ShouldBe(300m);
        destination.Currency.ShouldBe(Euro);
    }

    [Fact]
    public void Generation_ShouldKeepTheAmountsCoherentBetweenTheTwoLegs()
    {
        var transfer = SameCurrencyTransfer(300m);

        var postings = transfer.GeneratePostings();
        var source = postings.Single(posting => posting.AccountId == CheckingAccount.Id);
        var destination = postings.Single(posting => posting.AccountId == SavingsAccount.Id);

        Math.Abs(source.SignedAmount).ShouldBe(destination.SignedAmount);
        Math.Abs(source.SignedAmount).ShouldBe(300m);
    }

    [Fact]
    public void Construction_ShouldRejectTheSameAccount()
    {
        Should.Throw<ArgumentException>(
            () => SameCurrencyTransfer(CheckingAccount, CheckingAccount, 300m))
            .ParamName.ShouldBe("destinationAccount");
    }

    [Fact]
    public void Construction_ShouldRejectAccountsThatShareTheSameIdentifier()
    {
        var sameIdentifierAccount = new Account(
            CheckingAccount.Id, "Stesso identificatore", AccountType.Checking, Euro, AccountStatus.Active);

        Should.Throw<ArgumentException>(
            () => SameCurrencyTransfer(CheckingAccount, sameIdentifierAccount, 300m))
            .ParamName.ShouldBe("destinationAccount");
    }

    [Fact]
    public void Construction_ShouldRejectNonPositiveAmounts()
    {
        Should.Throw<ArgumentException>(() => SameCurrencyTransfer(0m));
        Should.Throw<ArgumentException>(() => SameCurrencyTransfer(-1m));
    }

    [Fact]
    public void Construction_ShouldRejectAnAmountInADifferentCurrencyThanTheSourceAccount()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                CheckingAccount,
                SavingsAccount,
                UsdAmount(300m),
                Eur(300m),
                null,
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("sourceAmount");
        exception.Message.ShouldContain("diversa dalla valuta del conto di origine");
    }

    [Fact]
    public void Construction_ShouldRejectAnAmountInADifferentCurrencyThanTheDestinationAccount()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                CheckingAccount,
                UsdAccount,
                Eur(300m),
                Eur(300m),
                null,
                OperationDate,
                RecordedAt));

        exception.ParamName.ShouldBe("destinationAmount");
        exception.Message.ShouldContain("diversa dalla valuta del conto di destinazione");
    }

    [Fact]
    public void Construction_ShouldRequireEqualAmountsWhenCurrenciesMatch()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                CheckingAccount,
                SavingsAccount,
                Eur(300m),
                Eur(200m),
                null,
                OperationDate,
                RecordedAt));

        exception.Message.ShouldContain("devono coincidere");
    }

    [Fact]
    public void ForeignCurrencyTransfer_ShouldGeneratePostingsInTheirCurrenciesWithoutAutomaticExchangeResults()
    {
        var transfer = ForeignTransfer(CheckingAccount, Eur(100m), UsdAccount, UsdAmount(108m), UsdRate);

        var postings = transfer.GeneratePostings();

        postings.Count.ShouldBe(2);
        var source = postings.Single(posting => posting.AccountId == CheckingAccount.Id);
        source.SignedAmount.ShouldBe(-100m);
        source.Currency.ShouldBe(Euro);
        var destination = postings.Single(posting => posting.AccountId == UsdAccount.Id);
        destination.SignedAmount.ShouldBe(108m);
        destination.Currency.ShouldBe(Usd);
        postings.ShouldAllBe(posting => posting.Kind == PostingKind.TransferLeg);
    }

    [Fact]
    public void ForeignCurrencyTransfer_ShouldRequireTheAppliedRate()
    {
        var exception = Should.Throw<ArgumentException>(
            () => ForeignTransfer(CheckingAccount, Eur(100m), UsdAccount, UsdAmount(108m), null));

        exception.ParamName.ShouldBe("appliedRate");
    }

    [Theory]
    [InlineData("GBP", "USD")]
    [InlineData("USD", "GBP")]
    public void ForeignCurrencyTransfer_ShouldRejectPairsThatDoNotIncludeEur(string sourceCode, string destinationCode)
    {
        var source = sourceCode == "GBP" ? GbpAccount : UsdAccount;
        var destination = sourceCode == "GBP" ? UsdAccount : GbpAccount;
        var sourceAmount = new PositiveMoney(new Money(100m, Currency.Parse(sourceCode)));
        var destinationAmount = new PositiveMoney(new Money(100m, Currency.Parse(destinationCode)));

        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                source,
                destination,
                sourceAmount,
                destinationAmount,
                UsdRate,
                OperationDate,
                RecordedAt));

        exception.Message.ShouldContain("EUR");
    }

    [Theory]
    [InlineData("EUR", "GBP")]
    [InlineData("GBP", "EUR")]
    public void ForeignCurrencyTransfer_ShouldRejectRatesWhoseQuotedCurrencyDoesNotMatchTheAccounts(string sourceCode, string destinationCode)
    {
        var source = sourceCode == "EUR" ? CheckingAccount : GbpAccount;
        var destination = destinationCode == "EUR" ? SavingsAccount : GbpAccount;
        var sourceAmount = new PositiveMoney(new Money(100m, Currency.Parse(sourceCode)));
        var destinationAmount = new PositiveMoney(new Money(100m, Currency.Parse(destinationCode)));

        var exception = Should.Throw<ArgumentException>(
            () => new Transfer(
                OperationId,
                source,
                destination,
                sourceAmount,
                destinationAmount,
                UsdRate,
                OperationDate,
                RecordedAt));

        exception.Message.ShouldContain("EUR");
    }

    [Fact]
    public void SameCurrencyTransfer_ShouldRejectAnAppliedRate()
    {
        var exception = Should.Throw<ArgumentException>(
            () => SameCurrencyTransfer(300m, UsdRate));

        exception.ParamName.ShouldBe("appliedRate");
    }

    [Fact]
    public void Balance_ShouldIntegrateThePostingsOfBothAccountsWithTheBalanceCalculator()
    {
        var transfer = SameCurrencyTransfer(300m);

        var postings = transfer.GeneratePostings();

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, OperationDate)
            .ShouldBe(new Money(-300m, Euro));
        AccountBalanceCalculator
            .Balance(SavingsAccount, postings, OperationDate)
            .ShouldBe(new Money(300m, Euro));
    }

    [Fact]
    public void Balance_ShouldIntegrateForeignCurrencyPostingsWithoutExchangeResults()
    {
        var transfer = ForeignTransfer(CheckingAccount, Eur(100m), UsdAccount, UsdAmount(108m), UsdRate);

        var postings = transfer.GeneratePostings();

        AccountBalanceCalculator
            .Balance(CheckingAccount, postings, OperationDate)
            .ShouldBe(new Money(-100m, Euro));
        AccountBalanceCalculator
            .Balance(UsdAccount, postings, OperationDate)
            .ShouldBe(new Money(108m, Usd));
    }

    private static Transfer SameCurrencyTransfer(decimal amount, ExchangeRate? appliedRate = null) =>
        SameCurrencyTransfer(CheckingAccount, SavingsAccount, amount, appliedRate);

    private static Transfer SameCurrencyTransfer(Account source, Account destination, decimal amount, ExchangeRate? appliedRate = null) =>
        new(OperationId, source, destination, Eur(amount), Eur(amount), appliedRate, OperationDate, RecordedAt);

    private static Transfer ForeignTransfer(
        Account source,
        PositiveMoney sourceAmount,
        Account destination,
        PositiveMoney destinationAmount,
        ExchangeRate? appliedRate) =>
        new(OperationId, source, destination, sourceAmount, destinationAmount, appliedRate, OperationDate, RecordedAt);

    private static PositiveMoney Eur(decimal amount) => new(new Money(amount, Euro));

    private static PositiveMoney UsdAmount(decimal amount) => new(new Money(amount, Usd));
}
