using Microsoft.JSInterop;

namespace MiddiFranchi.Web.Services;

/// <summary>
/// Ricorda nel browser le scelte di visualizzazione dei grafici, così restano
/// applicate dopo un ricaricamento della pagina.
/// </summary>
public sealed class ChartPreferences
{
    private readonly IJSRuntime _js;

    public ChartPreferences(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("mfGetPreference", key);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<bool?> GetBoolAsync(string key) => (await GetAsync(key)) switch
    {
        "1" => true,
        "0" => false,
        _ => null,
    };

    public Task SetAsync(string key, string value) => SetCoreAsync(key, value);

    public Task SetBoolAsync(string key, bool value) => SetCoreAsync(key, value ? "1" : "0");

    private async Task SetCoreAsync(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("mfSetPreference", key, value);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
