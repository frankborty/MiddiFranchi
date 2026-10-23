using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Domain.Portfolio;

public sealed class Instrument
{
    private const int MaximumSymbolLength = 32;
    private const int IsinLength = 12;
    private const int MaximumReferenceUrlLength = 500;
    private const int MaximumNotesLength = 1000;

    public Instrument(
        Guid id,
        string name,
        AssetClass assetClass,
        Currency quoteCurrency,
        PriceUnit priceUnit,
        Money? notionalValue = null,
        string? symbol = null,
        string? isin = null,
        string? referenceUrl = null,
        string? notes = null,
        InstrumentStatus status = InstrumentStatus.Active,
        DateOnly? maturityDate = null,
        decimal? couponRate = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identificativo dello strumento non può essere vuoto.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome dello strumento è obbligatorio.", nameof(name));
        }

        if (!Enum.IsDefined(assetClass))
        {
            throw new ArgumentException("La classe di attività dello strumento non è valida.", nameof(assetClass));
        }

        if (quoteCurrency.Code is null)
        {
            throw new ArgumentException("La valuta di quotazione dello strumento è obbligatoria.", nameof(quoteCurrency));
        }

        if (priceUnit == PriceUnit.PercentOfNominal)
        {
            ValidateNotionalValue(notionalValue, quoteCurrency);
        }
        else if (notionalValue is not null)
        {
            throw new ArgumentException(
                "Il valore nominale per unità è rilevante solo quando il prezzo unitario è espresso come percentuale del nominale.",
                nameof(notionalValue));
        }

        ValidateBondDetails(assetClass, maturityDate, couponRate);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("Lo stato dello strumento non è valido.", nameof(status));
        }

        Id = id;
        Name = name;
        AssetClass = assetClass;
        QuoteCurrency = quoteCurrency;
        PriceUnit = priceUnit;
        NotionalValue = notionalValue;
        Symbol = NormalizeSymbol(symbol);
        Isin = NormalizeIsin(isin);
        ReferenceUrl = NormalizeReferenceUrl(referenceUrl);
        Notes = NormalizeNotes(notes);
        Status = status;
        MaturityDate = maturityDate;
        CouponRate = couponRate;
    }

    public Guid Id { get; }

    public string Name { get; }

    public AssetClass AssetClass { get; }

    public Currency QuoteCurrency { get; }

    public PriceUnit PriceUnit { get; }

    public Money? NotionalValue { get; }

    /// <summary>Sigla di mercato opzionale (per esempio un ticker); assente se non registrata.</summary>
    public string? Symbol { get; }

    /// <summary>Codice ISIN normalizzato in maiuscolo; assente se non registrato.</summary>
    public string? Isin { get; }

    /// <summary>Link alla scheda descrittiva dello strumento; assente se non registrato.</summary>
    public string? ReferenceUrl { get; }

    /// <summary>Nota breve sullo strumento; assente se non registrata.</summary>
    public string? Notes { get; }

    public InstrumentStatus Status { get; }

    /// <summary>Data di scadenza, valorizzata solo per le obbligazioni.</summary>
    public DateOnly? MaturityDate { get; }

    /// <summary>Rendimento annuo fisso in percentuale, valorizzato solo per le obbligazioni.</summary>
    public decimal? CouponRate { get; }

    public bool IsActive => Status == InstrumentStatus.Active;

    public bool IsArchived => Status == InstrumentStatus.Archived;

    public Instrument WithStatus(InstrumentStatus status) => new(
        Id,
        Name,
        AssetClass,
        QuoteCurrency,
        PriceUnit,
        NotionalValue,
        Symbol,
        Isin,
        ReferenceUrl,
        Notes,
        status,
        MaturityDate,
        CouponRate);

    private static void ValidateNotionalValue(Money? notionalValue, Currency quoteCurrency)
    {
        if (notionalValue is null)
        {
            throw new ArgumentException(
                "Il valore nominale per unità è obbligatorio quando il prezzo unitario è espresso come percentuale del nominale.",
                nameof(notionalValue));
        }

        if (notionalValue.Value.Amount <= 0m)
        {
            throw new ArgumentException(
                $"Il valore nominale per unità deve essere positivo, ricevuto {notionalValue.Value}.",
                nameof(notionalValue));
        }

        if (notionalValue.Value.Currency != quoteCurrency)
        {
            throw new ArgumentException(
                $"La valuta del valore nominale ({notionalValue.Value.Currency.Code}) deve coincidere con la valuta di quotazione dello strumento ({quoteCurrency.Code}).",
                nameof(notionalValue));
        }
    }

    private static void ValidateBondDetails(AssetClass assetClass, DateOnly? maturityDate, decimal? couponRate)
    {
        if (assetClass != AssetClass.Bond)
        {
            if (maturityDate is not null)
            {
                throw new ArgumentException(
                    "La data di scadenza è ammessa solo per gli strumenti obbligazionari.",
                    nameof(maturityDate));
            }

            if (couponRate is not null)
            {
                throw new ArgumentException(
                    "Il rendimento annuo è ammesso solo per gli strumenti obbligazionari.",
                    nameof(couponRate));
            }

            return;
        }

        if (couponRate is { } rate && (rate <= 0m || rate > 100m))
        {
            throw new ArgumentException(
                $"Il rendimento annuo deve essere maggiore di zero e non superiore a 100, ricevuto {rate}.",
                nameof(couponRate));
        }
    }

    private static string? NormalizeSymbol(string? symbol)
    {
        var normalized = string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim();
        if (normalized is { Length: > MaximumSymbolLength })
        {
            throw new ArgumentException(
                $"La sigla dello strumento non può superare {MaximumSymbolLength} caratteri.",
                nameof(symbol));
        }

        return normalized;
    }

    private static string? NormalizeIsin(string? isin)
    {
        if (string.IsNullOrWhiteSpace(isin))
        {
            return null;
        }

        var normalized = isin.Trim().ToUpperInvariant();
        if (normalized.Length != IsinLength || !normalized.All(IsAsciiLetterOrDigit))
        {
            throw new ArgumentException(
                "Il codice ISIN deve essere composto da 12 caratteri alfanumerici.",
                nameof(isin));
        }

        return normalized;
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static string? NormalizeReferenceUrl(string? referenceUrl)
    {
        if (string.IsNullOrWhiteSpace(referenceUrl))
        {
            return null;
        }

        var normalized = referenceUrl.Trim();
        if (normalized.Length > MaximumReferenceUrlLength)
        {
            throw new ArgumentException(
                $"Il link alla scheda dello strumento non può superare {MaximumReferenceUrlLength} caratteri.",
                nameof(referenceUrl));
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Il link alla scheda dello strumento deve essere un indirizzo http o https valido.",
                nameof(referenceUrl));
        }

        return normalized;
    }

    private static string? NormalizeNotes(string? notes)
    {
        var normalized = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalized is { Length: > MaximumNotesLength })
        {
            throw new ArgumentException(
                $"La nota dello strumento non può superare {MaximumNotesLength} caratteri.",
                nameof(notes));
        }

        return normalized;
    }
}
