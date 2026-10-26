using System.Security.Cryptography;
using System.Text;

namespace MiddiFranchi.Application.Shared;

/// <summary>
/// Genera Guid deterministici da uno scope e da una chiave. Lo stesso input produce sempre
/// lo stesso Guid, così le entità derivate possono essere ricollegate senza memorizzare una
/// relazione esplicita.
/// </summary>
public static class DeterministicId
{
    private static readonly Guid Namespace = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    public static Guid From(string scope, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var normalized = $"{Namespace:N}|{scope.Trim().ToLowerInvariant()}|{key.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var bytes = hash[..16];

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
