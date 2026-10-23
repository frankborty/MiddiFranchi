using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Financial;

public sealed class InvestmentOperation : FinancialOperation
{
    private InvestmentOperation(
        Guid id,
        InvestmentOperationKind investmentKind,
        Instrument? instrument,
        Account settlementAccount,
        Quantity? quantity,
        QuotedPrice? quotedPrice,
        PositiveMoney? amount,
        IReadOnlyList<FeeComponent> fees,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description,
        string? notes)
        : base(id, operationDate, recordedAt, description, notes)
    {
        var feeList = fees.ToArray();
        var effectiveUnitPrice = Validate(
            investmentKind, instrument, settlementAccount, quantity, quotedPrice, amount, feeList);

        InvestmentKind = investmentKind;
        Instrument = instrument;
        SettlementAccount = settlementAccount;
        Quantity = quantity;
        QuotedPrice = quotedPrice;
        UnitPrice = effectiveUnitPrice;
        Amount = amount;
        Fees = feeList;
    }

    public InvestmentOperationKind InvestmentKind { get; }

    public Instrument? Instrument { get; }

    public Account SettlementAccount { get; }

    public Quantity? Quantity { get; }

    public QuotedPrice? QuotedPrice { get; }

    /// <summary>Prezzo unitario effettivo nella valuta di quotazione, già normalizzato per il nominale.</summary>
    public UnitPrice? UnitPrice { get; }

    public PositiveMoney? Amount { get; }

    public IReadOnlyList<FeeComponent> Fees { get; }

    public override OperationKind Kind => OperationKind.Investment;

    public static InvestmentOperation Buy(
        Guid id,
        Instrument instrument,
        Account settlementAccount,
        Quantity quantity,
        QuotedPrice quotedPrice,
        IReadOnlyList<FeeComponent>? fees,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null) =>
        ForKind(
            InvestmentOperationKind.Buy,
            id,
            instrument,
            settlementAccount,
            quantity,
            quotedPrice,
            null,
            fees,
            operationDate,
            recordedAt,
            description,
            notes);

    public static InvestmentOperation Sell(
        Guid id,
        Instrument instrument,
        Account settlementAccount,
        Quantity quantity,
        QuotedPrice quotedPrice,
        IReadOnlyList<FeeComponent>? fees,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null) =>
        ForKind(
            InvestmentOperationKind.Sell,
            id,
            instrument,
            settlementAccount,
            quantity,
            quotedPrice,
            null,
            fees,
            operationDate,
            recordedAt,
            description,
            notes);

    public static InvestmentOperation Dividend(
        Guid id,
        Instrument instrument,
        Account settlementAccount,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null) =>
        ForKind(
            InvestmentOperationKind.Dividend,
            id,
            instrument,
            settlementAccount,
            null,
            null,
            amount,
            [],
            operationDate,
            recordedAt,
            description,
            notes);

    public static InvestmentOperation Coupon(
        Guid id,
        Instrument instrument,
        Account settlementAccount,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description = null,
        string? notes = null) =>
        ForKind(
            InvestmentOperationKind.Coupon,
            id,
            instrument,
            settlementAccount,
            null,
            null,
            amount,
            [],
            operationDate,
            recordedAt,
            description,
            notes);

    public static InvestmentOperation StandaloneFee(
        Guid id,
        Account settlementAccount,
        PositiveMoney amount,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        Instrument? instrument = null,
        string? description = null,
        string? notes = null) =>
        ForKind(
            InvestmentOperationKind.StandaloneFee,
            id,
            instrument,
            settlementAccount,
            null,
            null,
            null,
            [new FeeComponent(FeeKind.Standalone, amount, settlementAccount)],
            operationDate,
            recordedAt,
            description,
            notes);

    protected override IReadOnlyList<AccountPosting> CreatePostings()
    {
        var postings = new List<AccountPosting>();

        switch (InvestmentKind)
        {
            case InvestmentOperationKind.Buy:
            case InvestmentOperationKind.Sell:
                var capital = UnitPrice!.Value * Quantity!.Value;
                postings.Add(new AccountPosting(
                    this,
                    SettlementAccount.Id,
                    PostingKind.Capital,
                    SettlementAccount.Currency,
                    SignedAmount(capital, SettlementAccount, credit: InvestmentKind == InvestmentOperationKind.Sell)));
                break;

            case InvestmentOperationKind.Dividend:
            case InvestmentOperationKind.Coupon:
                postings.Add(new AccountPosting(
                    this,
                    SettlementAccount.Id,
                    PostingKind.Income,
                    SettlementAccount.Currency,
                    SignedAmount(Amount!.Value.Value, SettlementAccount, credit: true)));
                break;
        }

        foreach (var fee in Fees)
        {
            postings.Add(new AccountPosting(
                this,
                fee.DebitAccount.Id,
                PostingKind.Fee,
                fee.DebitAccount.Currency,
                SignedAmount(fee.Amount.Value, fee.DebitAccount, credit: false)));
        }

        return postings;
    }

    private static InvestmentOperation ForKind(
        InvestmentOperationKind kind,
        Guid id,
        Instrument? instrument,
        Account settlementAccount,
        Quantity? quantity,
        QuotedPrice? quotedPrice,
        PositiveMoney? amount,
        IReadOnlyList<FeeComponent>? fees,
        DateOnly operationDate,
        DateTimeOffset recordedAt,
        string? description,
        string? notes) =>
        new(
            id,
            kind,
            instrument,
            settlementAccount,
            quantity,
            quotedPrice,
            amount,
            fees ?? [],
            operationDate,
            recordedAt,
            description,
            notes);

    private static UnitPrice? Validate(
        InvestmentOperationKind kind,
        Instrument? instrument,
        Account settlementAccount,
        Quantity? quantity,
        QuotedPrice? quotedPrice,
        PositiveMoney? amount,
        IReadOnlyList<FeeComponent> fees)
    {
        if (settlementAccount is null)
        {
            throw new ArgumentNullException(nameof(settlementAccount));
        }

        switch (kind)
        {
            case InvestmentOperationKind.Buy:
            case InvestmentOperationKind.Sell:
                if (instrument is null)
                {
                    throw new ArgumentException($"L'operazione {kind} richiede uno strumento finanziario.", nameof(instrument));
                }

                if (quantity is null || quantity.Value.Value <= 0m)
                {
                    throw new ArgumentException($"L'operazione {kind} richiede una quantità positiva.", nameof(quantity));
                }

                if (quotedPrice is null)
                {
                    throw new ArgumentException($"L'operazione {kind} richiede un prezzo quotato positivo.", nameof(quotedPrice));
                }

                if (quotedPrice.Value.Unit != instrument.PriceUnit)
                {
                    throw new ArgumentException(
                        $"Il prezzo quotato è di tipo {quotedPrice.Value.Unit}, diverso dall'unità di prezzo dello strumento {instrument.Name} ({instrument.PriceUnit}).",
                        nameof(quotedPrice));
                }

                if (quotedPrice.Value.PricePerUnit is { } quotedUnitPrice &&
                    quotedUnitPrice.Price.Currency != instrument.QuoteCurrency)
                {
                    throw new ArgumentException(
                        $"Valuta del prezzo unitario ({quotedUnitPrice.Price.Currency.Code}) diversa dalla valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}).",
                        nameof(quotedPrice));
                }

                var effectiveUnitPrice = quotedPrice.Value.EffectiveUnitPrice(instrument);

                if (effectiveUnitPrice.Price.Currency != instrument.QuoteCurrency)
                {
                    throw new ArgumentException(
                        $"Valuta del prezzo effettivo ({effectiveUnitPrice.Price.Currency.Code}) diversa dalla valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}).",
                        nameof(quotedPrice));
                }

                if (instrument.QuoteCurrency != settlementAccount.Currency)
                {
                    throw new ArgumentException(
                        $"In questa versione la valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}) deve coincidere con la valuta del conto di regolamento ({settlementAccount.Currency.Code}): i trasferimenti con valuta differente richiedono cambio applicato e importo contabilizzato e sono rinviati a un incremento successivo.",
                        nameof(instrument));
                }

                var expectedFeeKind = kind == InvestmentOperationKind.Buy ? FeeKind.BuyFee : FeeKind.SellFee;
                foreach (var fee in fees)
                {
                    if (fee.Kind != expectedFeeKind)
                    {
                        throw new ArgumentException(
                            $"Le commissioni dell'operazione {kind} devono essere di tipo {expectedFeeKind}.",
                            nameof(fees));
                    }

                    if (fee.DebitAccount.Id != settlementAccount.Id)
                    {
                        throw new ArgumentException(
                            "La commissione deve essere addebitata sul conto di regolamento dell'operazione d'investimento.",
                            nameof(fees));
                    }
                }

                return effectiveUnitPrice;

            case InvestmentOperationKind.Dividend:
            case InvestmentOperationKind.Coupon:
                if (instrument is null)
                {
                    throw new ArgumentException($"L'operazione {kind} richiede uno strumento finanziario.", nameof(instrument));
                }

                if (amount is null)
                {
                    throw new ArgumentException($"L'operazione {kind} richiede un provento positivo.", nameof(amount));
                }

                if (amount!.Value.Value.Currency != settlementAccount.Currency)
                {
                    throw new ArgumentException(
                        $"Valuta del provento ({amount!.Value.Value.Currency.Code}) diversa dalla valuta del conto di regolamento ({settlementAccount.Currency.Code}).",
                        nameof(amount));
                }

                if (quantity is not null)
                {
                    throw new ArgumentException($"L'operazione {kind} non ammette quantità.", nameof(quantity));
                }

                if (quotedPrice is not null)
                {
                    throw new ArgumentException($"L'operazione {kind} non ammette prezzo quotato.", nameof(quotedPrice));
                }

                if (fees.Count > 0)
                {
                    throw new ArgumentException($"L'operazione {kind} non ammette commissioni in questa versione.", nameof(fees));
                }

                return null;

            case InvestmentOperationKind.StandaloneFee:
                if (quantity is not null)
                {
                    throw new ArgumentException("Una commissione autonoma non ammette quantità.", nameof(quantity));
                }

                if (quotedPrice is not null)
                {
                    throw new ArgumentException("Una commissione autonoma non ammette prezzo quotato.", nameof(quotedPrice));
                }

                if (amount is not null)
                {
                    throw new ArgumentException("Una commissione autonoma non ammette provento indipendente dalla commissione.", nameof(amount));
                }

                if (fees.Count != 1 || fees[0].Kind != FeeKind.Standalone)
                {
                    throw new ArgumentException(
                        "Una commissione autonoma richiede esattamente una componente di commissione di tipo Standalone.",
                        nameof(fees));
                }

                if (fees[0].DebitAccount.Id != settlementAccount.Id)
                {
                    throw new ArgumentException(
                        "La commissione autonoma deve essere addebitata sul conto di regolamento indicato.",
                        nameof(fees));
                }

                return null;

            default:
                throw new NotSupportedException(
                    $"L'operazione d'investimento di tipo {kind} è un concetto futuro e non è supportata in questa versione.");
        }
    }
}
