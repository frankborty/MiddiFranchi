using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

/// <summary>
/// Risultato di uno strumento: plusvalenze realizzate, risultato non realizzato, proventi incassati
/// e commissioni autonome. Il risultato totale è disponibile solo quando il valore attuale è noto.
/// </summary>
public sealed record InvestmentResult
{
    public InvestmentResult(
        Instrument instrument,
        Quantity? quantity,
        Money costBasis,
        Money realizedResult,
        Money income,
        Money otherFees,
        Money? currentValue,
        Money? unrealizedResult,
        Money? totalResult,
        string? incompletenessReason)
    {
        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        EnsureCurrency(costBasis, instrument, nameof(costBasis));
        EnsureCurrency(realizedResult, instrument, nameof(realizedResult));
        EnsureCurrency(income, instrument, nameof(income));
        EnsureCurrency(otherFees, instrument, nameof(otherFees));
        if (currentValue is { } current)
        {
            EnsureCurrency(current, instrument, nameof(currentValue));
        }

        if (unrealizedResult is { } unrealized)
        {
            EnsureCurrency(unrealized, instrument, nameof(unrealizedResult));
        }

        if (totalResult is { } total)
        {
            EnsureCurrency(total, instrument, nameof(totalResult));
        }

        Instrument = instrument;
        Quantity = quantity;
        CostBasis = costBasis;
        RealizedResult = realizedResult;
        Income = income;
        OtherFees = otherFees;
        CurrentValue = currentValue;
        UnrealizedResult = unrealizedResult;
        TotalResult = totalResult;
        IncompletenessReason = incompletenessReason;
    }

    public Instrument Instrument { get; }

    /// <summary>Quantità residua; null quando la posizione è chiusa o non è mai stata aperta.</summary>
    public Quantity? Quantity { get; }

    public Money CostBasis { get; }

    public Money RealizedResult { get; }

    public Money Income { get; }

    public Money OtherFees { get; }

    public Money? CurrentValue { get; }

    public Money? UnrealizedResult { get; }

    public Money? TotalResult { get; }

    public string? IncompletenessReason { get; }

    public bool IsComplete => CurrentValue is not null;

    public bool IsOpen => Quantity is not null;

    private static void EnsureCurrency(Money money, Instrument instrument, string parameterName)
    {
        if (money.Currency != instrument.QuoteCurrency)
        {
            throw new ArgumentException(
                $"La valuta {money.Currency.Code} non coincide con la valuta di quotazione dello strumento ({instrument.QuoteCurrency.Code}).",
                parameterName);
        }
    }
}

/// <summary>
/// Insieme dei risultati per tutti gli strumenti. Le commissioni autonome senza strumento
/// restano separate e vengono sottratte dal risultato complessivo.
/// </summary>
public sealed record InvestmentResultSet
{
    private static readonly Currency Euro = Currency.Euro;

    public InvestmentResultSet(
        IReadOnlyList<InvestmentResult> assets,
        Money unattributedOtherFees)
    {
        if (assets is null)
        {
            throw new ArgumentNullException(nameof(assets));
        }

        if (unattributedOtherFees.Currency != Euro)
        {
            throw new ArgumentException(
                "Le commissioni autonome devono essere in EUR.",
                nameof(unattributedOtherFees));
        }

        Assets = assets.ToArray();
        UnattributedOtherFees = unattributedOtherFees;
    }

    public IReadOnlyList<InvestmentResult> Assets { get; }

    public Money UnattributedOtherFees { get; }

    public bool IsComplete => Assets.All(asset => asset.IsComplete);

    public Money? TotalResult => IsComplete
        ? new Money(
            Assets.Sum(asset => asset.TotalResult!.Value.Amount) - UnattributedOtherFees.Amount,
            Euro)
        : null;

    public Money RealizedResult => new(Assets.Sum(asset => asset.RealizedResult.Amount), Euro);

    public Money Income => new(Assets.Sum(asset => asset.Income.Amount), Euro);

    public Money OtherFees => new(
        Assets.Sum(asset => asset.OtherFees.Amount) + UnattributedOtherFees.Amount,
        Euro);
}
