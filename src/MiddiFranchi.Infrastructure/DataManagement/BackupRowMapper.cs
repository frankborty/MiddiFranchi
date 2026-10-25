using MiddiFranchi.Application.DataManagement.Backup;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.DataManagement;

internal static class BackupRowMapper
{
    public static AccountBackup ToBackup(AccountRecord record) => new(
        record.Id,
        record.Name,
        record.Type,
        record.CurrencyCode,
        record.Status,
        record.ColorHex,
        record.IconDataUrl);

    public static AccountRecord ToRecord(AccountBackup backup) => new()
    {
        Id = backup.Id,
        Name = backup.Name,
        Type = backup.Type,
        CurrencyCode = backup.CurrencyCode,
        Status = backup.Status,
        ColorHex = backup.ColorHex,
        IconDataUrl = backup.IconDataUrl,
    };

    public static CategoryBackup ToBackup(CategoryRecord record) => new(
        record.Id,
        record.Name,
        record.Type,
        record.Description,
        record.Subcategory,
        record.Icon,
        record.ColorHex);

    public static CategoryRecord ToRecord(CategoryBackup backup) => new()
    {
        Id = backup.Id,
        Name = backup.Name,
        Type = backup.Type,
        Description = backup.Description,
        Subcategory = backup.Subcategory,
        Icon = backup.Icon,
        ColorHex = backup.ColorHex,
    };

    public static InstrumentBackup ToBackup(InstrumentRecord record) => new(
        record.Id,
        record.Name,
        record.Symbol,
        record.Isin,
        record.AssetClass,
        record.Status,
        record.QuoteCurrencyCode,
        record.PriceUnit,
        record.NotionalValueAmount,
        record.NotionalValueCurrencyCode,
        record.ReferenceUrl,
        record.Notes,
        record.MaturityDate,
        record.CouponRate);

    public static InstrumentRecord ToRecord(InstrumentBackup backup) => new()
    {
        Id = backup.Id,
        Name = backup.Name,
        Symbol = backup.Symbol,
        Isin = backup.Isin,
        AssetClass = backup.AssetClass,
        Status = backup.Status,
        QuoteCurrencyCode = backup.QuoteCurrencyCode,
        PriceUnit = backup.PriceUnit,
        NotionalValueAmount = backup.NotionalValueAmount,
        NotionalValueCurrencyCode = backup.NotionalValueCurrencyCode,
        ReferenceUrl = backup.ReferenceUrl,
        Notes = backup.Notes,
        MaturityDate = backup.MaturityDate,
        CouponRate = backup.CouponRate,
    };

    public static FinancialOperationBackup ToBackup(FinancialOperationRecord record) => new(
        record.Id,
        record.Kind,
        record.OperationDate,
        record.RecordedAt,
        record.Description,
        record.OriginalOperationId,
        record.AccountId,
        record.CategoryId,
        record.AmountAmount,
        record.AmountCurrencyCode,
        record.SourceAccountId,
        record.DestinationAccountId,
        record.SourceAmountAmount,
        record.SourceAmountCurrencyCode,
        record.DestinationAmountAmount,
        record.DestinationAmountCurrencyCode,
        record.InvestmentKind,
        record.InstrumentId,
        record.SettlementAccountId,
        record.QuantityValue,
        record.QuotedPriceUnit,
        record.QuotedPricePriceAmount,
        record.QuotedPricePriceCurrencyCode,
        record.QuotedPricePercentOfNominal);

    public static FinancialOperationRecord ToRecord(FinancialOperationBackup backup) => new()
    {
        Id = backup.Id,
        Kind = backup.Kind,
        OperationDate = backup.OperationDate,
        RecordedAt = backup.RecordedAt,
        Description = backup.Description,
        OriginalOperationId = backup.OriginalOperationId,
        AccountId = backup.AccountId,
        CategoryId = backup.CategoryId,
        AmountAmount = backup.AmountAmount,
        AmountCurrencyCode = backup.AmountCurrencyCode,
        SourceAccountId = backup.SourceAccountId,
        DestinationAccountId = backup.DestinationAccountId,
        SourceAmountAmount = backup.SourceAmountAmount,
        SourceAmountCurrencyCode = backup.SourceAmountCurrencyCode,
        DestinationAmountAmount = backup.DestinationAmountAmount,
        DestinationAmountCurrencyCode = backup.DestinationAmountCurrencyCode,
        InvestmentKind = backup.InvestmentKind,
        InstrumentId = backup.InstrumentId,
        SettlementAccountId = backup.SettlementAccountId,
        QuantityValue = backup.QuantityValue,
        QuotedPriceUnit = backup.QuotedPriceUnit,
        QuotedPricePriceAmount = backup.QuotedPricePriceAmount,
        QuotedPricePriceCurrencyCode = backup.QuotedPricePriceCurrencyCode,
        QuotedPricePercentOfNominal = backup.QuotedPricePercentOfNominal,
    };

    public static AccountPostingBackup ToBackup(AccountPostingRecord record) => new(
        record.FinancialOperationId,
        record.AccountId,
        record.Kind,
        record.CurrencyCode,
        record.SignedAmount);

    public static AccountPostingRecord ToRecord(AccountPostingBackup backup) => new()
    {
        FinancialOperationId = backup.FinancialOperationId,
        AccountId = backup.AccountId,
        Kind = backup.Kind,
        CurrencyCode = backup.CurrencyCode,
        SignedAmount = backup.SignedAmount,
    };

    public static FeeComponentBackup ToBackup(FeeComponentRecord record) => new(
        record.InvestmentOperationId,
        record.Ordinal,
        record.Kind,
        record.AmountAmount,
        record.AmountCurrencyCode,
        record.DebitAccountId);

    public static FeeComponentRecord ToRecord(FeeComponentBackup backup) => new()
    {
        InvestmentOperationId = backup.InvestmentOperationId,
        Ordinal = backup.Ordinal,
        Kind = backup.Kind,
        AmountAmount = backup.AmountAmount,
        AmountCurrencyCode = backup.AmountCurrencyCode,
        DebitAccountId = backup.DebitAccountId,
    };

    public static PriceObservationBackup ToBackup(PriceObservationRecord record) => new(
        record.Id,
        record.InstrumentId,
        record.Unit,
        record.Price,
        record.PriceCurrencyCode,
        record.ValidAt,
        record.ObservedAt,
        record.Source);

    public static PriceObservationRecord ToRecord(PriceObservationBackup backup) => new()
    {
        Id = backup.Id,
        InstrumentId = backup.InstrumentId,
        Unit = backup.Unit,
        Price = backup.Price,
        PriceCurrencyCode = backup.PriceCurrencyCode,
        ValidAt = backup.ValidAt,
        ObservedAt = backup.ObservedAt,
        Source = backup.Source,
    };

    public static ManualBalanceBackup ToBackup(ManualBalanceRecord record) => new(
        record.Id,
        record.AccountId,
        record.AmountAmount,
        record.AmountCurrencyCode,
        record.ValidAt,
        record.RecordedAt);

    public static ManualBalanceRecord ToRecord(ManualBalanceBackup backup) => new()
    {
        Id = backup.Id,
        AccountId = backup.AccountId,
        AmountAmount = backup.AmountAmount,
        AmountCurrencyCode = backup.AmountCurrencyCode,
        ValidAt = backup.ValidAt,
        RecordedAt = backup.RecordedAt,
    };

    public static LoanBackup ToBackup(LoanRecord record) => new(
        record.Id,
        record.AccountId,
        record.Borrower,
        record.CreatedAt);

    public static LoanRecord ToRecord(LoanBackup backup) => new()
    {
        Id = backup.Id,
        AccountId = backup.AccountId,
        Borrower = backup.Borrower,
        CreatedAt = backup.CreatedAt,
    };

    public static LoanReadingBackup ToBackup(LoanReadingRecord record) => new(
        record.Id,
        record.LoanId,
        record.AmountAmount,
        record.AmountCurrencyCode,
        record.ValidAt,
        record.RecordedAt);

    public static LoanReadingRecord ToRecord(LoanReadingBackup backup) => new()
    {
        Id = backup.Id,
        LoanId = backup.LoanId,
        AmountAmount = backup.AmountAmount,
        AmountCurrencyCode = backup.AmountCurrencyCode,
        ValidAt = backup.ValidAt,
        RecordedAt = backup.RecordedAt,
    };
}
