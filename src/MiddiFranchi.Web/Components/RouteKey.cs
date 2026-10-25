using System.Globalization;
using System.Text;

namespace MiddiFranchi.Web.Components;

/// <summary>
/// Costruisce gli identificativi leggibili usati negli URL a partire dai nomi.
/// Se due elementi condividono lo stesso slug viene aggiunto un suffisso numerico
/// deterministico, ordinato per id, così i link restano stabili.
/// </summary>
public static class RouteKey
{
    public static string From(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length == 0 ? fallback : builder.ToString();
    }

    public static IReadOnlyDictionary<Guid, string> BuildKeys<T>(
        IEnumerable<T> items,
        Func<T, Guid> id,
        Func<T, string> name,
        string fallback)
    {
        var entries = items
            .Select(item => (Id: id(item), Slug: From(name(item), fallback)))
            .ToArray();

        var keys = new Dictionary<Guid, string>(entries.Length);
        foreach (var group in entries.GroupBy(entry => entry.Slug, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(entry => entry.Id).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                keys[ordered[index].Id] = ordered.Length == 1
                    ? ordered[index].Slug
                    : $"{ordered[index].Slug}-{index + 1}";
            }
        }

        return keys;
    }

    public static Guid? FindByKey<T>(
        IEnumerable<T> items,
        Func<T, Guid> id,
        Func<T, string> name,
        string fallback,
        string key)
    {
        if (Guid.TryParse(key, out var parsed))
        {
            return parsed;
        }

        foreach (var pair in BuildKeys(items, id, name, fallback))
        {
            if (string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9';
}
