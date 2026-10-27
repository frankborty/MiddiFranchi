using System.Text;
using System.Text.Json;
using MiddiFranchi.Application.Persistence;

namespace MiddiFranchi.Web.Services;

public enum AppSection
{
    Dashboard,
    Accounts,
    Investments,
    Operations,
    Settings,
}

public sealed record SectionAppearance(string Icon, string Color);

/// <summary>
/// Icona e colore delle sezioni principali, persistiti in AppMetadata e condivisi da
/// navigazione, intestazioni e componenti che usano i colori di sezione.
/// </summary>
public sealed class SectionAppearanceService
{
    private const string StorageKey = "ui.section-appearance.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<AppSection, SectionAppearance> Defaults =
        new Dictionary<AppSection, SectionAppearance>
        {
            [AppSection.Dashboard] = new("chart", "#8a8780"),
            [AppSection.Accounts] = new("wallet", "#0ea5e9"),
            [AppSection.Investments] = new("pie", "#4f5bd5"),
            [AppSection.Operations] = new("receipt", "#b86b35"),
            [AppSection.Settings] = new("settings", "#6f6b5e"),
        };
    private static readonly IReadOnlyDictionary<AppSection, string> LegacyDefaults =
        new Dictionary<AppSection, string>
        {
            [AppSection.Dashboard] = "#2456c9",
            [AppSection.Accounts] = "#0ea5e9",
            [AppSection.Investments] = "#8b5cf6",
            [AppSection.Operations] = "#f59e0b",
            [AppSection.Settings] = "#64748b",
        };

    private readonly IAppMetadataRepository _metadata;
    private readonly Dictionary<AppSection, SectionAppearance> _items = new(Defaults);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loaded;

    public SectionAppearanceService(IAppMetadataRepository metadata)
    {
        _metadata = metadata;
    }

    public event Action? Changed;

    public IReadOnlyDictionary<AppSection, SectionAppearance> Items => _items;

    public SectionAppearance Get(AppSection section) =>
        _items.TryGetValue(section, out var appearance)
            ? appearance
            : Defaults[section];

    public static SectionAppearance DefaultFor(AppSection section) => Defaults[section];

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            var json = await _metadata.GetValueAsync(StorageKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var stored = JsonSerializer.Deserialize<Dictionary<string, SectionAppearance>>(
                        json,
                        JsonOptions);
                    if (stored is not null)
                    {
                        foreach (var (key, value) in stored)
                        {
                            if (Enum.TryParse<AppSection>(key, ignoreCase: true, out var section))
                            {
                                _items[section] = Normalize(section, value);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }

            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<AppSection, SectionAppearance> values,
        CancellationToken cancellationToken = default)
    {
        foreach (var (section, value) in values)
        {
            _items[section] = Normalize(section, value);
        }

        var payload = _items.ToDictionary(
            pair => pair.Key.ToString(),
            pair => pair.Value,
            StringComparer.Ordinal);
        await _metadata.SetAsync(
            StorageKey,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
        _loaded = true;
        Changed?.Invoke();
    }

    public string CssVariables
    {
        get
        {
            var dashboard = Get(AppSection.Dashboard).Color;
            var accounts = Get(AppSection.Accounts).Color;
            var investments = Get(AppSection.Investments).Color;
            var operations = Get(AppSection.Operations).Color;
            var settings = Get(AppSection.Settings).Color;
            var builder = new StringBuilder();

            Append(builder, "--mf-dashboard-color", dashboard);
            Append(builder, "--mf-nav-home", OnDark(dashboard));
            Append(builder, "--mf-link", OnLight(dashboard));
            Append(builder, "--mf-brand-text", OnLight(dashboard));
            Append(builder, "--mf-blue-100", Tint(dashboard));
            Append(builder, "--mf-accounts", accounts);
            Append(builder, "--mf-accounts-strong", OnLight(accounts, 70));
            Append(builder, "--mf-accounts-text", OnLight(accounts, 55));
            Append(builder, "--mf-accounts-100", Tint(accounts));
            Append(builder, "--mf-nav-accounts", OnDark(accounts));
            Append(builder, "--mf-investments", investments);
            Append(builder, "--mf-investments-strong", OnLight(investments, 70));
            Append(builder, "--mf-investments-text", OnLight(investments, 55));
            Append(builder, "--mf-investments-100", Tint(investments));
            Append(builder, "--mf-nav-investments", OnDark(investments));
            Append(builder, "--mf-amber-500", operations);
            Append(builder, "--mf-amber-100", Tint(operations));
            Append(builder, "--mf-accent-text", OnLight(operations, 55));
            Append(builder, "--mf-nav-operations", OnDark(operations));
            Append(builder, "--mf-settings-color", settings);
            Append(builder, "--mf-nav-settings", OnDark(settings));
            return builder.ToString();
        }
    }

    private static SectionAppearance Normalize(AppSection section, SectionAppearance appearance)
    {
        var color = IsHexColor(appearance.Color)
            ? appearance.Color.ToLowerInvariant()
            : Defaults[section].Color;
        if (LegacyDefaults.TryGetValue(section, out var legacyColor) && color == legacyColor)
        {
            color = Defaults[section].Color;
        }

        if (section == AppSection.Dashboard && color == "#6f8f2f")
        {
            color = Defaults[section].Color;
        }

        if (section == AppSection.Accounts && color == "#2f6f7d")
        {
            color = Defaults[section].Color;
        }

        return new SectionAppearance(
            string.IsNullOrWhiteSpace(appearance.Icon)
                ? Defaults[section].Icon
                : appearance.Icon.Trim(),
            color);
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static string Tint(string color) =>
        $"color-mix(in srgb, {color} 14%, var(--mf-surface))";

    private static string OnLight(string color, int percent = 55) =>
        $"color-mix(in srgb, {color} {percent}%, var(--mf-text))";

    private static string OnDark(string color) =>
        $"color-mix(in srgb, {color} 55%, white)";

    private static void Append(StringBuilder builder, string name, string value) =>
        builder.Append(name).Append(':').Append(value).Append(';');
}
