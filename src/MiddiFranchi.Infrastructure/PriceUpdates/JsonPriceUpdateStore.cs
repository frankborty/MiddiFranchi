using System.Text.Json;
using System.Text.Json.Serialization;
using MiddiFranchi.Application.Financial;

namespace MiddiFranchi.Infrastructure.PriceUpdates;

public sealed class JsonPriceUpdateStore : IPriceUpdateStore
{
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
    }
}
