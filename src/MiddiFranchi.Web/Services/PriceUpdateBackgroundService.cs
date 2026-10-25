using MiddiFranchi.Application.Financial;

namespace MiddiFranchi.Web.Services;

public sealed class PriceUpdateBackgroundService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPriceUpdateStore _store;
    private readonly ILogger<PriceUpdateBackgroundService> _logger;

    public PriceUpdateBackgroundService(
        IServiceScopeFactory scopeFactory,
        IPriceUpdateStore store,
        ILogger<PriceUpdateBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(PollInterval);
            do
            {
                try
                {
                    await CheckAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Errore durante il controllo dell'aggiornamento automatico dei prezzi.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        var settings = _store.GetSettings();
        if (!settings.Enabled)
        {
            return;
        }

        var status = _store.GetStatus();
        if (status.IsRunning)
        {
            return;
        }

        if (!PriceUpdateScheduler.IsDue(settings, status, DateTimeOffset.Now))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PriceUpdateService>();
        await service.UpdateAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
