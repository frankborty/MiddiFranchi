using System.Security.Cryptography;
using System.Text;

namespace MiddiFranchi.Application.DataImport;

/// <summary>
/// Converte gli identificativi esterni della vecchia applicazione in Guid deterministici.
/// Lo stesso valore (tipo entità + external_id) produce sempre lo stesso Guid, così
/// una seconda importazione aggiorna le stesse righe invece di duplicarle.
/// </summary>
public static class ExternalIdMapper
{
    public const string AccountEntity = "account";
    public const string AccountBalanceEntity = "account-balance";
    public const string InstrumentEntity = "instrument";
    public const string InstrumentPriceEntity = "instrument-price";
    public const string OperationEntity = "operation";

    private static readonly Guid Namespace = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    public static Guid ToGuid(string entity, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var normalized = $"{Namespace:N}|{entity.Trim().ToLowerInvariant()}|{externalId.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var bytes = hash[..16];

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
