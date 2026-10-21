using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;

namespace MiddiFranchi.Infrastructure.Persistence.Entities;

public sealed class AccountRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public AccountType Type { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public AccountStatus Status { get; set; }

    public string? ColorHex { get; set; }

    public string? IconDataUrl { get; set; }
}

public sealed class CategoryRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public CategoryType Type { get; set; }

    public string? Description { get; set; }
}

public sealed class InvestmentPortfolioRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string ReferenceCurrencyCode { get; set; } = null!;
}

public sealed class InstrumentRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Symbol { get; set; }

    public InstrumentType Type { get; set; }

    public string QuoteCurrencyCode { get; set; } = null!;

    public PriceUnit PriceUnit { get; set; }

    public decimal? NotionalValueAmount { get; set; }

    public string? NotionalValueCurrencyCode { get; set; }
}

public sealed class FinancialOperationRecord
{
    public Guid Id { get; set; }

    public OperationKind Kind { get; set; }

    public DateOnly OperationDate { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public Guid? OriginalOperationId { get; set; }

    public Guid? AccountId { get; set; }

    public AccountRecord? Account { get; set; }

    public Guid? CategoryId { get; set; }

    public CategoryRecord? Category { get; set; }

    public decimal? AmountAmount { get; set; }

    public string? AmountCurrencyCode { get; set; }

    public Guid? SourceAccountId { get; set; }

    public AccountRecord? SourceAccount { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public AccountRecord? DestinationAccount { get; set; }

    public decimal? SourceAmountAmount { get; set; }

    public string? SourceAmountCurrencyCode { get; set; }

    public decimal? DestinationAmountAmount { get; set; }

    public string? DestinationAmountCurrencyCode { get; set; }

    public InvestmentOperationKind? InvestmentKind { get; set; }

    public Guid? PortfolioId { get; set; }

    public InvestmentPortfolioRecord? Portfolio { get; set; }

    public Guid? InstrumentId { get; set; }

    public InstrumentRecord? Instrument { get; set; }

    public Guid? SettlementAccountId { get; set; }

    public AccountRecord? SettlementAccount { get; set; }

    public decimal? QuantityValue { get; set; }

    public PriceUnit? QuotedPriceUnit { get; set; }

    public decimal? QuotedPricePriceAmount { get; set; }

    public string? QuotedPricePriceCurrencyCode { get; set; }

    public decimal? QuotedPricePercentOfNominal { get; set; }

    public ICollection<AccountPostingRecord> Postings { get; set; } = new List<AccountPostingRecord>();

    public ICollection<FeeComponentRecord> Fees { get; set; } = new List<FeeComponentRecord>();
}

public sealed class AccountPostingRecord
{
    public long Id { get; set; }

    public Guid FinancialOperationId { get; set; }

    public FinancialOperationRecord Operation { get; set; } = null!;

    public Guid AccountId { get; set; }

    public AccountRecord Account { get; set; } = null!;

    public PostingKind Kind { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal SignedAmount { get; set; }
}

public sealed class FeeComponentRecord
{
    public long Id { get; set; }

    public Guid InvestmentOperationId { get; set; }

    public FinancialOperationRecord InvestmentOperation { get; set; } = null!;

    public int Ordinal { get; set; }

    public FeeKind Kind { get; set; }

    public decimal AmountAmount { get; set; }

    public string AmountCurrencyCode { get; set; } = null!;

    public Guid DebitAccountId { get; set; }

    public AccountRecord DebitAccount { get; set; } = null!;
}

public sealed class PriceObservationRecord
{
    public Guid Id { get; set; }

    public Guid InstrumentId { get; set; }

    public InstrumentRecord Instrument { get; set; } = null!;

    public PriceUnit Unit { get; set; }

    public decimal Price { get; set; }

    public string? PriceCurrencyCode { get; set; }

    public DateOnly ValidAt { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public string Source { get; set; } = null!;
}

public sealed class ManualBalanceRecord
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountRecord Account { get; set; } = null!;

    public decimal AmountAmount { get; set; }

    public string AmountCurrencyCode { get; set; } = null!;

    public DateOnly ValidAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class LoanRecord
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public AccountRecord Account { get; set; } = null!;

    public string Borrower { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<LoanReadingRecord> Readings { get; set; } = new List<LoanReadingRecord>();
}

public sealed class LoanReadingRecord
{
    public Guid Id { get; set; }

    public Guid LoanId { get; set; }

    public LoanRecord Loan { get; set; } = null!;

    public decimal AmountAmount { get; set; }

    public string AmountCurrencyCode { get; set; } = null!;

    public DateOnly ValidAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}

