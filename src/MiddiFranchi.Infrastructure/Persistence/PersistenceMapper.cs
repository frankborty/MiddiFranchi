using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence;

internal static class PersistenceMapper
{
    public static AccountRecord ToRecord(Account account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Type = account.Type,
        CurrencyCode = account.Currency.Code,
        Status = account.Status,
        ColorHex = account.ColorHex,
        IconDataUrl = account.IconDataUrl,
    };

    public static Account ToDomain(AccountRecord record) => new(
        record.Id,
        record.Name,
        record.Type,
        Currency.Parse(record.CurrencyCode),
        record.Status,
        record.ColorHex,
        record.IconDataUrl);

    public static CategoryRecord ToRecord(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Type = category.Type,
        Description = category.Description,
        Subcategory = category.Subcategory,
        Icon = category.Icon,
        ColorHex = category.ColorHex,
    };

    public static Category ToDomain(CategoryRecord record) => new(
        record.Id,
        record.Name,
        record.Type,
        record.Description,
        record.Subcategory,
        record.Icon,
        record.ColorHex);

    public static InstrumentRecord ToRecord(Instrument instrument) => new()
    {
        Id = instrument.Id,
        Name = instrument.Name,
        Symbol = instrument.Symbol,
        Isin = instrument.Isin,
        AssetClass = instrument.AssetClass,
        Status = instrument.Status,
        QuoteCurrencyCode = instrument.QuoteCurrency.Code,
        PriceUnit = instrument.PriceUnit,
        NotionalValueAmount = instrument.NotionalValue?.Amount,
        NotionalValueCurrencyCode = instrument.NotionalValue?.Currency.Code,
        ReferenceUrl = instrument.ReferenceUrl,
        Notes = instrument.Notes,
        MaturityDate = instrument.MaturityDate,
        CouponRate = instrument.CouponRate,
        TaxRate = instrument.TaxRate,
    };

    public static Instrument ToDomain(InstrumentRecord record)
    {
        var notional = record.NotionalValueAmount is null
            ? (Money?)null
            : new Money(
                record.NotionalValueAmount.Value,
                Currency.Parse(record.NotionalValueCurrencyCode
                    ?? throw new InvalidOperationException(
                        $"Lo strumento {record.Id} ha un valore nominale senza valuta.")));

        return new Instrument(
            record.Id,
            record.Name,
            record.AssetClass,
            Currency.Parse(record.QuoteCurrencyCode),
            record.PriceUnit,
            notional,
            record.Symbol,
            record.Isin,
            record.ReferenceUrl,
            record.Notes,
            record.Status,
            record.MaturityDate,
            record.CouponRate,
            record.TaxRate);
    }

    public static PriceObservationRecord ToRecord(PriceObservation observation) => new()
    {
        Id = observation.Id,
        InstrumentId = observation.Instrument.Id,
        Unit = observation.Unit,
        Price = observation.Price,
        PriceCurrencyCode = observation.PriceCurrency?.Code,
        ValidAt = observation.ValidAt,
        ObservedAt = observation.ObservedAt,
        Source = observation.Source,
    };

    public static PriceObservation ToDomain(PriceObservationRecord record, Instrument instrument) =>
        record.Unit switch
        {
            PriceUnit.PerUnit => PriceObservation.AtUnitPrice(
                record.Id,
                instrument,
                new UnitPrice(new Money(
                    record.Price,
                    Currency.Parse(record.PriceCurrencyCode
                        ?? throw new InvalidOperationException(
                            $"La quotazione {record.Id} è per unità ma non ha valuta.")))),
                record.ValidAt,
                record.ObservedAt,
                record.Source),
            PriceUnit.PercentOfNominal => PriceObservation.AsPercentOfNominal(
                record.Id,
                instrument,
                record.Price,
                record.ValidAt,
                record.ObservedAt,
                record.Source),
            _ => throw new InvalidOperationException(
                $"La quotazione {record.Id} ha un'unità di prezzo non supportata: {record.Unit}."),
        };

    public static FinancialOperationRecord ToRecord(FinancialOperation operation)
    {
        var record = new FinancialOperationRecord
        {
            Id = operation.Id,
            Kind = operation.Kind,
            OperationDate = operation.OperationDate,
            RecordedAt = operation.RecordedAt,
            Description = operation.Description,
            OriginalOperationId = operation.OriginalOperationId,
        };

        switch (operation)
        {
            case Income income:
                ApplyAmount(record, income.Amount);
                record.AccountId = income.Account?.Id;
                record.CategoryId = income.Category.Id;
                record.InstrumentId = income.Instrument?.Id;
                break;

            case Expense expense:
                ApplyAmount(record, expense.Amount);
                record.AccountId = expense.Account?.Id;
                record.CategoryId = expense.Category.Id;
                break;

            case Transfer transfer:
                record.SourceAccountId = transfer.SourceAccount.Id;
                record.DestinationAccountId = transfer.DestinationAccount.Id;
                record.SourceAmountAmount = transfer.SourceAmount.Value.Amount;
                record.SourceAmountCurrencyCode = transfer.SourceAmount.Value.Currency.Code;
                record.DestinationAmountAmount = transfer.DestinationAmount.Value.Amount;
                record.DestinationAmountCurrencyCode = transfer.DestinationAmount.Value.Currency.Code;
                break;

            case InvestmentOperation investment:
                record.InvestmentKind = investment.InvestmentKind;
                record.InstrumentId = investment.Instrument?.Id;
                record.SettlementAccountId = investment.SettlementAccount?.Id;
                record.QuantityValue = investment.Quantity?.Value;
                ApplyQuotedPrice(record, investment.QuotedPrice);

                if (investment.Amount is not null)
                {
                    ApplyAmount(record, investment.Amount.Value);
                }

                var ordinal = 0;
                foreach (var fee in investment.Fees)
                {
                    record.Fees.Add(new FeeComponentRecord
                    {
                        InvestmentOperationId = investment.Id,
                        Ordinal = ordinal++,
                        Kind = fee.Kind,
                        AmountAmount = fee.Amount.Value.Amount,
                        AmountCurrencyCode = fee.Amount.Value.Currency.Code,
                        DebitAccountId = fee.DebitAccount?.Id,
                    });
                }

                break;

            case ReversalOperation:
                break;
        }

        return record;
    }

    public static IReadOnlyList<AccountPostingRecord> ToPostingRecords(FinancialOperation operation) =>
        operation.GeneratePostings()
            .Select(posting => new AccountPostingRecord
            {
                FinancialOperationId = operation.Id,
                AccountId = posting.AccountId,
                Kind = posting.Kind,
                CurrencyCode = posting.Currency.Code,
                SignedAmount = posting.SignedAmount,
            })
            .ToArray();

    public static IReadOnlyList<FinancialOperation> ToDomainOperations(
        IReadOnlyCollection<FinancialOperationRecord> records,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, Category> categories,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        IReadOnlyDictionary<Guid, IReadOnlyList<FeeComponent>> fees)
    {
        var result = new Dictionary<Guid, FinancialOperation>();

        foreach (var record in records)
        {
            if (record.Kind != OperationKind.Investment &&
                fees.TryGetValue(record.Id, out var invalidFees) &&
                invalidFees.Count > 0)
            {
                throw new InvalidOperationException(
                    $"L'operazione {record.Id} non è un'operazione d'investimento ma ha commissioni associate.");
            }
        }

        foreach (var record in records.Where(record => record.Kind != OperationKind.Reversal))
        {
            result[record.Id] = ToDomainOperation(record, accounts, categories, instruments, fees);
        }

        foreach (var record in records.Where(record => record.Kind == OperationKind.Reversal))
        {
            var originalId = record.OriginalOperationId
                ?? throw new InvalidOperationException(
                    $"Lo storno {record.Id} non indica l'operazione originale.");

            if (!result.TryGetValue(originalId, out var original))
            {
                throw new InvalidOperationException(
                    $"Lo storno {record.Id} riferisce l'operazione {originalId}, che non è stata caricata.");
            }

            result[record.Id] = new ReversalOperation(
                record.Id,
                original,
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null);
        }

        return result.Values
            .OrderBy(operation => operation.OperationDate)
            .ThenBy(operation => operation.RecordedAt)
            .ThenBy(operation => operation.Id)
            .ToArray();
    }

    private static FinancialOperation ToDomainOperation(
        FinancialOperationRecord record,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, Category> categories,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        IReadOnlyDictionary<Guid, IReadOnlyList<FeeComponent>> fees)
    {
        return record.Kind switch
        {
            OperationKind.Income => new Income(
                record.Id,
                OptionalAccount(accounts, record.AccountId, record.Id),
                RequiredCategory(categories, record.CategoryId, record.Id),
                RequiredAmount(record, record.Id),
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null,
                OptionalInstrument(instruments, record.InstrumentId, record.Id)),
            OperationKind.Expense => new Expense(
                record.Id,
                OptionalAccount(accounts, record.AccountId, record.Id),
                RequiredCategory(categories, record.CategoryId, record.Id),
                RequiredAmount(record, record.Id),
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null),
            OperationKind.Transfer => new Transfer(
                record.Id,
                RequiredAccount(accounts, record.SourceAccountId, record.Id),
                RequiredAccount(accounts, record.DestinationAccountId, record.Id),
                RequiredPositiveMoney(record.SourceAmountAmount, record.SourceAmountCurrencyCode, record.Id),
                RequiredPositiveMoney(record.DestinationAmountAmount, record.DestinationAmountCurrencyCode, record.Id),
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null),
            OperationKind.Investment => ToDomainInvestment(record, accounts, instruments, fees),
            _ => throw new InvalidOperationException(
                $"Il tipo di operazione {record.Kind} non è supportato dalla persistenza."),
        };
    }

    private static Instrument? OptionalInstrument(
        IReadOnlyDictionary<Guid, Instrument> instruments,
        Guid? instrumentId,
        Guid operationId)
    {
        if (instrumentId is null)
        {
            return null;
        }

        return instruments.TryGetValue(instrumentId.Value, out var instrument)
            ? instrument
            : throw new InvalidOperationException(
                $"Lo strumento dell'operazione {operationId} non è stato caricato.");
    }

    private static InvestmentOperation ToDomainInvestment(
        FinancialOperationRecord record,
        IReadOnlyDictionary<Guid, Account> accounts,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        IReadOnlyDictionary<Guid, IReadOnlyList<FeeComponent>> fees)
    {
        var investmentKind = record.InvestmentKind
            ?? throw new InvalidOperationException(
                $"L'operazione d'investimento {record.Id} non ha un tipo.");
        var settlementAccount = OptionalAccount(accounts, record.SettlementAccountId, record.Id);
        var feeList = fees.TryGetValue(record.Id, out var foundFees) ? foundFees : [];

        Instrument? instrument = null;
        if (record.InstrumentId is not null)
        {
            if (!instruments.TryGetValue(record.InstrumentId.Value, out instrument))
            {
                throw new InvalidOperationException(
                    $"Lo strumento dell'operazione {record.Id} non è stato caricato.");
            }
        }

        return investmentKind switch
        {
            InvestmentOperationKind.Buy or InvestmentOperationKind.Sell => CreateBuyOrSell(
                record,
                investmentKind,
                instrument ?? throw new InvalidOperationException(
                    $"L'operazione {record.Id} richiede uno strumento."),
                settlementAccount,
                new Quantity(record.QuantityValue
                    ?? throw new InvalidOperationException(
                        $"L'operazione {record.Id} non ha una quantità.")),
                RequiredQuotedPrice(record),
                feeList),
            InvestmentOperationKind.Dividend => CreateDividendOrCoupon(
                investmentKind,
                record,
                instrument ?? throw new InvalidOperationException(
                    $"L'operazione {record.Id} richiede uno strumento."),
                settlementAccount,
                feeList),
            InvestmentOperationKind.Coupon => CreateDividendOrCoupon(
                investmentKind,
                record,
                instrument ?? throw new InvalidOperationException(
                    $"L'operazione {record.Id} richiede uno strumento."),
                settlementAccount,
                feeList),
            InvestmentOperationKind.StandaloneFee => InvestmentOperation.StandaloneFee(
                record.Id,
                settlementAccount,
                StandaloneFeeAmount(record, settlementAccount, feeList),
                record.OperationDate,
                record.RecordedAt,
                instrument,
                record.Description,
                null),
            _ => throw new InvalidOperationException(
                $"Il tipo di operazione d'investimento {investmentKind} non è supportato dalla persistenza."),
        };
    }

    private static InvestmentOperation CreateBuyOrSell(
        FinancialOperationRecord record,
        InvestmentOperationKind kind,
        Instrument instrument,
        Account? settlementAccount,
        Quantity quantity,
        QuotedPrice quotedPrice,
        IReadOnlyList<FeeComponent> fees)
    {
        return kind == InvestmentOperationKind.Buy
            ? InvestmentOperation.Buy(
                record.Id,
                instrument,
                settlementAccount,
                quantity,
                quotedPrice,
                fees,
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null)
            : InvestmentOperation.Sell(
                record.Id,
                instrument,
                settlementAccount,
                quantity,
                quotedPrice,
                fees,
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null);
    }

    private static InvestmentOperation CreateDividendOrCoupon(
        InvestmentOperationKind kind,
        FinancialOperationRecord record,
        Instrument instrument,
        Account? settlementAccount,
        IReadOnlyList<FeeComponent> fees)
    {
        var amount = RequiredAmount(record, record.Id);

        return kind == InvestmentOperationKind.Dividend
            ? InvestmentOperation.Dividend(
                record.Id,
                instrument,
                settlementAccount,
                amount,
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null,
                fees)
            : InvestmentOperation.Coupon(
                record.Id,
                instrument,
                settlementAccount,
                amount,
                record.OperationDate,
                record.RecordedAt,
                record.Description,
                null,
                fees);
    }

    private static void ApplyAmount(FinancialOperationRecord record, PositiveMoney amount)
    {
        record.AmountAmount = amount.Value.Amount;
        record.AmountCurrencyCode = amount.Value.Currency.Code;
    }

    private static void ApplyQuotedPrice(FinancialOperationRecord record, QuotedPrice? quotedPrice)
    {
        if (quotedPrice is null)
        {
            return;
        }

        record.QuotedPriceUnit = quotedPrice.Value.Unit;

        if (quotedPrice.Value.PricePerUnit is not null)
        {
            record.QuotedPricePriceAmount = quotedPrice.Value.PricePerUnit.Value.Price.Amount;
            record.QuotedPricePriceCurrencyCode = quotedPrice.Value.PricePerUnit.Value.Price.Currency.Code;
        }

        record.QuotedPricePercentOfNominal = quotedPrice.Value.PercentOfNominal;
    }

    private static QuotedPrice RequiredQuotedPrice(FinancialOperationRecord record) =>
        record.QuotedPriceUnit switch
        {
            PriceUnit.PerUnit => QuotedPrice.AtUnitPrice(new UnitPrice(new Money(
                record.QuotedPricePriceAmount
                    ?? throw new InvalidOperationException($"L'operazione {record.Id} non ha il prezzo unitario."),
                Currency.Parse(record.QuotedPricePriceCurrencyCode
                    ?? throw new InvalidOperationException($"L'operazione {record.Id} non ha la valuta del prezzo."))))),
            PriceUnit.PercentOfNominal => QuotedPrice.AsPercentOfNominal(
                record.QuotedPricePercentOfNominal
                    ?? throw new InvalidOperationException($"L'operazione {record.Id} non ha la quotazione percentuale.")),
            _ => throw new InvalidOperationException(
                $"L'operazione {record.Id} non ha un prezzo quotato valido."),
        };

    private static PositiveMoney StandaloneFeeAmount(
        FinancialOperationRecord record,
        Account? settlementAccount,
        IReadOnlyList<FeeComponent> fees)
    {
        if (fees.Count != 1 || fees[0].Kind != FeeKind.Standalone)
        {
            throw new InvalidOperationException(
                $"La commissione autonoma {record.Id} non ha esattamente una componente Standalone.");
        }

        if (settlementAccount is not null &&
            fees[0].DebitAccount is { } debitAccount &&
            debitAccount.Id != settlementAccount.Id)
        {
            throw new InvalidOperationException(
                $"La commissione autonoma {record.Id} non è addebitata sul conto di regolamento.");
        }

        return fees[0].Amount;
    }

    private static Account? OptionalAccount(
        IReadOnlyDictionary<Guid, Account> accounts,
        Guid? accountId,
        Guid operationId)
    {
        if (accountId is null)
        {
            return null;
        }

        return accounts.TryGetValue(accountId.Value, out var account)
            ? account
            : throw new InvalidOperationException(
                $"Il conto dell'operazione {operationId} non è stato caricato.");
    }

    private static Account RequiredAccount(
        IReadOnlyDictionary<Guid, Account> accounts,
        Guid? accountId,
        Guid operationId)
    {
        if (accountId is null || !accounts.TryGetValue(accountId.Value, out var account))
        {
            throw new InvalidOperationException(
                $"Il conto dell'operazione {operationId} non è stato caricato.");
        }

        return account;
    }

    private static Category RequiredCategory(
        IReadOnlyDictionary<Guid, Category> categories,
        Guid? categoryId,
        Guid operationId)
    {
        if (categoryId is null || !categories.TryGetValue(categoryId.Value, out var category))
        {
            throw new InvalidOperationException(
                $"La categoria dell'operazione {operationId} non è stata caricata.");
        }

        return category;
    }

    public static ManualBalanceRecord ToRecord(ManualBalance reading) => new()
    {
        Id = reading.Id,
        AccountId = reading.Account.Id,
        AmountAmount = reading.Amount.Amount,
        AmountCurrencyCode = reading.Amount.Currency.Code,
        ValidAt = reading.ValidAt,
        RecordedAt = reading.RecordedAt,
    };

    public static ManualBalance ToDomain(
        ManualBalanceRecord record,
        IReadOnlyDictionary<Guid, Account> accounts) =>
        new(
            record.Id,
            accounts.TryGetValue(record.AccountId, out var account)
                ? account
                : throw new InvalidOperationException(
                    $"Il conto della rilevazione {record.Id} non è stato caricato."),
            new Money(record.AmountAmount, Currency.Parse(record.AmountCurrencyCode)),
            record.ValidAt,
            record.RecordedAt);

    public static LoanRecord ToRecord(Loan loan) => new()
    {
        Id = loan.Id,
        AccountId = loan.AccountId,
        Borrower = loan.Borrower,
        CreatedAt = loan.CreatedAt,
    };

    public static Loan ToDomain(LoanRecord record, Account account) =>
        new(record.Id, account, record.Borrower, record.CreatedAt);

    public static LoanReadingRecord ToRecord(LoanReading reading) => new()
    {
        Id = reading.Id,
        LoanId = reading.LoanId,
        AmountAmount = reading.Amount.Amount,
        AmountCurrencyCode = reading.Amount.Currency.Code,
        ValidAt = reading.ValidAt,
        RecordedAt = reading.RecordedAt,
        Note = reading.Note,
    };

    public static LoanReading ToDomain(LoanReadingRecord record, Loan loan) =>
        new(
            record.Id,
            loan,
            new Money(record.AmountAmount, Currency.Parse(record.AmountCurrencyCode)),
            record.ValidAt,
            record.RecordedAt,
            record.Note);

    private static PositiveMoney RequiredAmount(FinancialOperationRecord record, Guid operationId) =>
        RequiredPositiveMoney(record.AmountAmount, record.AmountCurrencyCode, operationId);

    private static PositiveMoney RequiredPositiveMoney(decimal? amount, string? currencyCode, Guid operationId)
    {
        if (amount is null || currencyCode is null)
        {
            throw new InvalidOperationException(
                $"L'importo dell'operazione {operationId} non è completo.");
        }

        return new PositiveMoney(new Money(amount.Value, Currency.Parse(currencyCode)));
    }
}
