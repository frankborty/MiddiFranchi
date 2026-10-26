using System.Text.Json;
using Microsoft.JSInterop;

namespace MiddiFranchi.Web.Services;

/// <summary>
/// Persistenza dei filtri di pagina nel browser, così da ritrovarli tornando indietro.
/// </summary>
public sealed class FilterPreferences
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly IJSRuntime _js;

    public FilterPreferences(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        string? json;
        try
        {
            json = await _js.InvokeAsync<string?>("mfGetPreference", key);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            await _js.InvokeVoidAsync(
                "mfSetPreference",
                key,
                JsonSerializer.Serialize(value, Options));
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
