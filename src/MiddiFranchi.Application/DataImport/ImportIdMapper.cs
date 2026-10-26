using System.Globalization;
using MiddiFranchi.Application.Shared;
using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Application.DataImport;

/// <summary>
/// Converte le chiavi naturali usate nell'importazione in Guid deterministici.
/// La stessa chiave (tipo entità + valori) produce sempre lo stesso Guid, così una
/// seconda importazione aggiorna le stesse righe invece di duplicarle.
/// </summary>
public static class ImportIdMapper
{
    public const string AccountEntity = "account";
    public const string AccountBalanceEntity = "account-balance";
    public const string InstrumentEntity = "instrument";
    public const string InstrumentPriceEntity = "instrument-price";
    public const string OperationEntity = "operation";

    public static Guid ToGuid(string entity, string key) => DeterministicId.From(entity, key);

    /// <summary>
    /// Identificativo stabile per i saldi importati: il saldo è identificato dal nome
    /// del conto e dalla data di riferimento.
    /// </summary>
    public static Guid ToAccountBalanceGuid(string accountName, DateOnly validAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var key = $"{accountName.Trim().ToLowerInvariant()}|" +
                  validAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ToGuid(AccountBalanceEntity, key);
    }

    /// <summary>
    /// Identificativo stabile per gli strumenti importati: lo strumento è identificato
    /// dal codice ISIN normalizzato.
    /// </summary>
    public static Guid ToInstrumentGuidByIsin(string isin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isin);

        return ToGuid(InstrumentEntity, "isin:" + isin.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Chiave naturale di una quotazione: strumento e data di validità.
    /// </summary>
    public static string InstrumentPriceKey(Guid instrumentId, DateOnly validAt) =>
        $"{instrumentId:N}|{validAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Identificativo stabile di una quotazione importata: reimportando la stessa
    /// coppia strumento/data la quotazione viene aggiornata invece di duplicarla.
    /// </summary>
    public static Guid ToInstrumentPriceGuid(Guid instrumentId, DateOnly validAt) =>
        ToGuid(InstrumentPriceEntity, InstrumentPriceKey(instrumentId, validAt));

    /// <summary>
    /// Chiave naturale di un'operazione: strumento, tipo, data, quantità, prezzo e
    /// commissioni. Viene usata per riconoscere le operazioni già presenti.
    /// </summary>
    public static string OperationKey(
        Guid instrumentId,
        InvestmentOperationKind kind,
        DateOnly date,
        decimal quantity,
        decimal price,
        decimal fee) =>
        $"{instrumentId:N}|{kind}|{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}|" +
        $"{FormatNumber(quantity)}|{FormatNumber(price)}|{FormatNumber(fee)}";

    public static Guid ToOperationGuid(
        Guid instrumentId,
        InvestmentOperationKind kind,
        DateOnly date,
        decimal quantity,
        decimal price,
        decimal fee) =>
        ToGuid(
            OperationEntity,
            OperationKey(instrumentId, kind, date, quantity, price, fee));

    private static string FormatNumber(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}
