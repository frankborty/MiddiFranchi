using System.Text.Json;
using System.Text.Json.Serialization;
using MiddiFranchi.Application.DataManagement;
using MiddiFranchi.Application.Financial;

namespace MiddiFranchi.Infrastructure.PriceUpdates;

public sealed class JsonPriceUpdateStore : IPriceUpdateStore, IPriceUpdateBackupParticipant
{
    private const int HistoryLimit = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _sync = new();
    private readonly string _path;
    private Document _document;

    public JsonPriceUpdateStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiddiFranchi");
        _path = Path.Combine(directory, "price-update.json");
        _document = Load();
    }

    public PriceUpdateSettings GetSettings()
    {
        lock (_sync)
        {
            return _document.Settings ?? PriceUpdateSettings.Default;
        }
    }

    public void SaveSettings(PriceUpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            _document.Settings = settings;
            Save();
        }
    }

    public PriceUpdateStatus GetStatus()
    {
        lock (_sync)
        {
            return _document.Status ?? PriceUpdateStatus.Empty;
        }
    }

    public IReadOnlyList<PriceUpdateRun> GetHistory()
    {
        lock (_sync)
        {
            return (_document.History ?? []).Take(HistoryLimit).ToArray();
        }
    }

    public void RecordRun(PriceUpdateRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        lock (_sync)
        {
            var history = _document.History ?? [];
            history.Insert(0, run);
            if (history.Count > HistoryLimit)
            {
                history.RemoveRange(HistoryLimit, history.Count - HistoryLimit);
            }

            _document.History = history;
            Save();
        }
    }

    public string ExportDocument()
    {
        lock (_sync)
        {
            return JsonSerializer.Serialize(_document, JsonOptions);
        }
    }

    public void ImportDocument(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = JsonSerializer.Deserialize<Document>(json, JsonOptions) ?? new Document();

        lock (_sync)
        {
            _document = new Document
            {
                Settings = document.Settings,
                Status = document.Status is { } status
                    ? status with { IsRunning = false }
                    : null,
                History = document.History is { } history
                    ? history.Take(HistoryLimit).ToList()
                    : null,
            };
            Save();
        }
    }

    public void SaveStatus(PriceUpdateStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        lock (_sync)
        {
            _document.Status = status;
            Save();
        }
    }

    private Document Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Document();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Document>(json, JsonOptions) ?? new Document();
        }
        catch (Exception)
        {
            return new Document();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_document, JsonOptions);
            var temporaryPath = _path + "." + Path.GetRandomFileName() + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception)
        {
            // La persistenza è best effort: lo stato in memoria resta valido.
        }
    }

    private sealed class Document
    {
        public PriceUpdateSettings? Settings { get; set; }

        public PriceUpdateStatus? Status { get; set; }

        public List<PriceUpdateRun>? History { get; set; }
    }
}
