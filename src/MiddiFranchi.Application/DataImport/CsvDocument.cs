using System.Text;

namespace MiddiFranchi.Application.DataImport;

/// <summary>
/// Parser CSV minimale conforme a RFC 4180: separatore virgola, campi tra virgolette,
/// virgolette escapate e campi multiriga. La prima riga contiene le intestazioni.
/// </summary>
public sealed class CsvDocument
{
    private readonly IReadOnlyList<string> _headers;
    private readonly IReadOnlyList<CsvRow> _rows;

    private CsvDocument(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        _headers = headers;
        _rows = rows;
    }

    public IReadOnlyList<string> Headers => _headers;

    public IReadOnlyList<CsvRow> Rows => _rows;

    public static CsvDocument Parse(string content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);

        var records = ParseRecords(content, fileName);
        if (records.Count == 0)
        {
            throw new ImportValidationException($"{fileName}: il file è vuoto o manca l'intestazione.");
        }

        var (headerLine, headerFields) = records[0];
        var headers = new List<string>(headerFields.Count);
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headerFields.Count; index++)
        {
            var header = headerFields[index].Trim();
            if (string.IsNullOrEmpty(header))
            {
                throw new ImportValidationException(
                    $"{fileName}, riga {headerLine}: intestazione vuota nella colonna {index + 1}.");
            }

            if (!indexes.TryAdd(header, index))
            {
                throw new ImportValidationException(
                    $"{fileName}, riga {headerLine}: intestazione duplicata «{header}».");
            }

            headers.Add(header);
        }

        var rows = new List<CsvRow>(records.Count - 1);
        foreach (var (line, fields) in records.Skip(1))
        {
            if (fields.Count > headers.Count)
            {
                throw new ImportValidationException(
                    $"{fileName}, riga {line}: {fields.Count} campi ma l'intestazione ne dichiara {headers.Count}.");
            }

            var values = new string?[headers.Count];
            for (var index = 0; index < headers.Count; index++)
            {
                values[index] = index < fields.Count ? fields[index] : null;
            }

            rows.Add(new CsvRow(headers, indexes, values, line, fileName));
        }

        return new CsvDocument(headers, rows);
    }

    public void EnsureColumns(params string[] requiredColumns)
    {
        var missing = requiredColumns
            .Where(column => !_headers.Contains(column, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new ImportValidationException(
                $"Intestazioni mancanti: {string.Join(", ", missing)}.");
        }
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string Write(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(Escape)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        }

        return builder.ToString();
    }

    private static List<(int Line, List<string> Fields)> ParseRecords(
        string content,
        string fileName)
    {
        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStartLine = 1;

        void FinishField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void FinishRow()
        {
            FinishField();
            if (fields.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                records.Add((rowStartLine, fields));
            }

            fields = [];
            rowStartLine = line;
        }

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];

            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (character == '\r')
                {
                    if (index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        continue;
                    }

                    field.Append(character);
                }
                else
                {
                    if (character == '\n')
                    {
                        line++;
                    }

                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    FinishField();
                    break;
                case '\r':
                    break;
                case '\n':
                    FinishRow();
                    line++;
                    rowStartLine = line;
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (inQuotes)
        {
            throw new ImportValidationException(
                $"{fileName}, riga {rowStartLine}: campo tra virgolette non chiuso.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            FinishRow();
        }

        return records;
    }
}

public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, int> _indexes;
    private readonly string?[] _values;
    private readonly string _fileName;

    public CsvRow(
        IReadOnlyList<string> headers,
        IReadOnlyDictionary<string, int> indexes,
        string?[] values,
        int line,
        string fileName)
    {
        Headers = headers;
        _indexes = indexes;
        _values = values;
        Line = line;
        _fileName = fileName;
    }

    public int Line { get; }

    public string FileName => _fileName;

    public IReadOnlyList<string> Headers { get; }

    public string? Get(string column)
    {
        if (!_indexes.TryGetValue(column, out var index) || index >= _values.Length)
        {
            return null;
        }

        return _values[index];
    }

    public string? GetTrimmed(string column) => Get(column)?.Trim();

    public string Required(string column)
    {
        var value = GetTrimmed(column);
        if (string.IsNullOrEmpty(value))
        {
            throw new ImportValidationException(
                $"{_fileName}, riga {Line}: il campo «{column}» è obbligatorio.");
        }

        return value;
    }
}
