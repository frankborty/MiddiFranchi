using Microsoft.JSInterop;

namespace MiddiFranchi.Web.Services;

/// <summary>
/// Mostra i messaggi di esito in notifiche temporanee (toast) che scompaiono da sole
/// oppure possono essere chiuse dall'utente.
/// </summary>
public sealed class NotificationService
{
    private readonly IJSRuntime _js;

    public NotificationService(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask Success(string message) => ShowAsync("success", message);

    public ValueTask Info(string message) => ShowAsync("info", message);

    public ValueTask Warning(string message) => ShowAsync("warning", message);

    public ValueTask Error(string message) => ShowAsync("error", message);

    private async ValueTask ShowAsync(string tone, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await _js.InvokeVoidAsync("mfToast", message, tone);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
            // Il circuito interattivo non è ancora pronto (prerender): la notifica è superflua.
        }
    }
}
