using System.Text;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;

namespace MiddiFranchi.Application.DataImport;

internal sealed record AccountsImportRow(
    string Name,
    AccountType Type,
    AccountStatus Status,
    string? ColorHex,
    string? IconDataUrl,
    IReadOnlyList<string> Loans,
    int Line);

internal sealed class ParsedAccountsFile
{
    public List<AccountsImportRow> Rows { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Errors { get; } = [];
}

internal static class AccountsImportParser
{
    public static ParsedAccountsFile Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string text;
        using (var reader = new StreamReader(
                   source,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: true,
                   bufferSize: 81920,
                   leaveOpen: true))
        {
            text = reader.ReadToEnd();
        }

        return ReadText(text);
    }

    public static ParsedAccountsFile ReadText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new ParsedAccountsFile();
        CsvDocument document;
        try
        {
            document = CsvDocument.Parse(text, ImportFileNames.Accounts);
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add(exception.Message);
            return result;
        }

        try
        {
            document.EnsureColumns(
                "name", "type", "status", "color_hex", "icon");
        }
        catch (ImportValidationException exception)
        {
            result.Errors.Add($"{ImportFileNames.Accounts}: {exception.Message}");
            return result;
        }

        var seenNames = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var row in document.Rows)
        {
            try
            {
                var name = row.Required("name");
                if (seenNames.TryGetValue(name, out var firstLine))
                {
                    result.Errors.Add(
                        $"{ImportFileNames.Accounts}, riga {row.Line}: " +
                        $"nome conto duplicato «{name}» (già definito alla riga {firstLine}).");
                    continue;
                }

                seenNames[name] = row.Line;
                var type = ImportFileParser.ParseEnum<AccountType>(row, "type");
                var status = ImportFileParser.ParseOptionalEnum(row, "status", AccountStatus.Active);
                var color = row.GetTrimmed("color_hex");
                var icon = ImportFileParser.NormalizeIcon(row.GetTrimmed("icon"));
                var loans = ParseLoans(row);

                _ = new Account(
                    Guid.NewGuid(),
                    name,
                    type,
                    Currency.Euro,
                    status,
                    color,
                    icon);

                result.Rows.Add(new AccountsImportRow(
                    name, type, status, color, icon, loans, row.Line));
            }
            catch (ImportValidationException exception)
            {
                result.Errors.Add(exception.Message);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                result.Errors.Add(
                    $"{ImportFileNames.Accounts}, riga {row.Line}: {exception.Message}");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseLoans(CsvRow row)
    {
        var value = row.GetTrimmed("loans");
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var type = ImportFileParser.ParseEnum<AccountType>(row, "type");
        if (type != AccountType.Loans)
        {
            throw new ImportValidationException(
                $"{row.FileName}, riga {row.Line}: i prestiti possono essere " +
                "registrati solo su conti di tipo Loans.");
        }

        var loans = new List<string>();
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var token in value.Split(';'))
        {
            var name = token.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ImportValidationException(
                    $"{row.FileName}, riga {row.Line}: il campo «loans» contiene " +
                    "un nome di prestito vuoto.");
            }

            if (!seen.Add(name))
            {
                throw new ImportValidationException(
                    $"{row.FileName}, riga {row.Line}: nome di prestito duplicato " +
                    $"«{name}» nel campo «loans».");
            }

            loans.Add(name);
        }

        return loans;
    }
}
