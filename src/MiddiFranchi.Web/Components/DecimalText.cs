using System.Globalization;
using System.Text;

namespace MiddiFranchi.Web.Components;

/// <summary>
/// Formattazione e lettura dei numeri decimali dei form. Le interfacce usano la virgola
/// come separatore decimale; in lettura viene accettato anche il punto per compatibilità.
/// </summary>
public static class DecimalText
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    public static string Format(decimal value) =>
        value.ToString("0.########", Italian);

    public static string Format(decimal? value) =>
        value is { } number ? Format(number) : "";

    public static bool TryParse(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return decimal.TryParse(
            Normalize(text),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim())
        {
            if (char.IsWhiteSpace(character) || character is '\'' or '\u00A0' or '\u202F')
            {
                continue;
            }

            builder.Append(character);
        }

        var normalized = builder.ToString();
        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            return lastComma > lastDot
                ? normalized.Replace(".", string.Empty).Replace(',', '.')
                : normalized.Replace(",", string.Empty);
        }

        if (lastComma >= 0)
        {
            return normalized.Count(character => character == ',') == 1
                ? normalized.Replace(',', '.')
                : normalized.Replace(",", string.Empty);
        }

        return normalized.Count(character => character == '.') > 1
            ? normalized.Replace(".", string.Empty)
            : normalized;
    }
}
