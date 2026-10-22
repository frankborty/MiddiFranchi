using System.Text.RegularExpressions;

namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Propone un nome progressivo quando l'utente crea un elemento con un nome già utilizzato.
/// </summary>
public static partial class NameUniqueness
{
    public static string? SuggestIfDuplicate(
        string requested,
        IEnumerable<string> existing,
        StringComparer? comparer = null)
    {
        var name = requested.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        comparer ??= StringComparer.CurrentCultureIgnoreCase;
        var names = existing
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .ToHashSet(comparer);
        if (!names.Contains(name))
        {
            return null;
        }

        var baseName = NumberedSuffix().Replace(name, "").TrimEnd();
        if (baseName.Length == 0)
        {
            baseName = name;
        }

        var index = 2;
        while (names.Contains($"{baseName} ({index})"))
        {
            index++;
        }

        return $"{baseName} ({index})";
    }

    [GeneratedRegex(@"\s+\(\d+\)$")]
    private static partial Regex NumberedSuffix();
}
