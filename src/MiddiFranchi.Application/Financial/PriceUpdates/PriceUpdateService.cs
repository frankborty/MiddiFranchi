using MiddiFranchi.Application.DataManagement;

namespace MiddiFranchi.Application.Financial;

public sealed class PriceUpdateService
{
    private readonly InvestmentWorkspaceService _workspace;
    private readonly InvestmentAssetService _assets;
    private readonly IInvestmentQuoteProvider _quotes;
    private readonly IPriceUpdateStore _store;
    private readonly DataMaintenanceGate _gate;

    public PriceUpdateService(
        InvestmentWorkspaceService workspace,
        InvestmentAssetService assets,
        IInvestmentQuoteProvider quotes,
        IPriceUpdateStore store,
        DataMaintenanceGate gate)
    {
        _workspace = workspace;
        _assets = assets;
        _quotes = quotes;
        _store = store;
        _gate = gate;
    }

    public async Task<PriceUpdateResult> UpdateInstrumentAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (!_gate.TryEnter(out var handle))
        {
            return PriceUpdateResult.Failure(
                instrumentId,
                "",
                "Un aggiornamento o una manutenzione dei dati è già in corso. Riprova tra poco.");
        }

        try
        {
            var workspace = await _workspace.LoadAsync(cancellationToken).ConfigureAwait(false);
            var asset = workspace.Assets.FirstOrDefault(item => item.InstrumentId == instrumentId);
            if (asset is null)
            {
                return PriceUpdateResult.Failure(instrumentId, "", "Strumento non trovato.");
            }

            var result = await UpdateAssetAsync(asset, _store.GetSettings(), cancellationToken)
                .ConfigureAwait(false);
            _store.RecordRun(new PriceUpdateRun(
                DateTimeOffset.Now,
                PriceUpdateTrigger.Manual,
                PriceUpdateScope.Instrument,
                asset.InstrumentId,
                asset.Name,
                result.Success ? 1 : 0,
                result.Success ? 0 : 1,
                result.Success ? null : result.Error,
                [result]));

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _store.RecordRun(new PriceUpdateRun(
                DateTimeOffset.Now,
                PriceUpdateTrigger.Manual,
                PriceUpdateScope.Instrument,
                instrumentId,
                null,
                0,
                1,
                exception.Message));

            return PriceUpdateResult.Failure(instrumentId, "", exception.Message);
        }
        finally
        {
            handle?.Dispose();
        }
    }

    public async Task<IReadOnlyList<PriceUpdateResult>> UpdateAllAsync(
        PriceUpdateTrigger trigger = PriceUpdateTrigger.Manual,
        CancellationToken cancellationToken = default)
    {
        if (!_gate.TryEnter(out var handle))
        {
            return
            [
                PriceUpdateResult.Failure(
                    Guid.Empty,
                    "",
                    "Un aggiornamento o una manutenzione dei dati è già in corso. Riprova tra poco."),
            ];
        }

        var previous = _store.GetStatus();
        try
        {
            _store.SaveStatus(previous with
            {
                IsRunning = true,
                LastError = null,
                Results = [],
            });

            var workspace = await _workspace.LoadAsync(cancellationToken).ConfigureAwait(false);
            var settings = _store.GetSettings();
            var assets = workspace.Assets
                .Where(asset => !asset.IsArchived && !string.IsNullOrWhiteSpace(asset.Isin))
                .OrderBy(asset => asset.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            var results = new List<PriceUpdateResult>(assets.Length);
            foreach (var asset in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await UpdateAssetAsync(asset, settings, cancellationToken)
                    .ConfigureAwait(false));
            }

            var finishedAt = DateTimeOffset.Now;
            var lastError = results.FirstOrDefault(result => !result.Success)?.Error;
            _store.SaveStatus(new PriceUpdateStatus(
                LastRunAt: finishedAt,
                LastSuccessAt: results.Any(result => result.Success)
                    ? finishedAt
                    : previous.LastSuccessAt,
                IsRunning: false,
                LastError: lastError,
                Results: results));
            _store.RecordRun(new PriceUpdateRun(
                finishedAt,
                trigger,
                PriceUpdateScope.All,
                null,
                null,
                results.Count(result => result.Success),
                results.Count(result => !result.Success),
                lastError,
                results));

            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _store.SaveStatus(previous with { IsRunning = false });
            throw;
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.Now;
            _store.SaveStatus(previous with
            {
                LastRunAt = failedAt,
                IsRunning = false,
                LastError = exception.Message,
                Results = [],
            });
            _store.RecordRun(new PriceUpdateRun(
                failedAt,
                trigger,
                PriceUpdateScope.All,
                null,
                null,
                0,
                0,
                exception.Message));

            return
            [
                PriceUpdateResult.Failure(Guid.Empty, "", exception.Message),
            ];
        }
        finally
        {
            handle?.Dispose();
        }
    }

    private async Task<PriceUpdateResult> UpdateAssetAsync(
        InvestmentAssetRow asset,
        PriceUpdateSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.Isin))
        {
            return PriceUpdateResult.Failure(asset.InstrumentId, asset.Name, "ISIN non impostato.");
        }

        try
        {
            var quote = await _quotes.GetQuoteAsync(asset.Isin, settings.Exchange, cancellationToken)
                .ConfigureAwait(false);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var validAt = quote.AsOf is { } asOf && asOf <= today ? asOf : today;

            await _assets.RecordPriceAsync(
                new RecordPriceRequest(
                    asset.InstrumentId,
                    quote.Price,
                    validAt,
                    "Borsa Italiana",
                    quote.LastTradeTime),
                cancellationToken).ConfigureAwait(false);

            return new PriceUpdateResult(
                asset.InstrumentId,
                asset.Name,
                Success: true,
                quote.Price,
                validAt,
                Error: null,
                asset.PriceUnit,
                asset.Symbol,
                quote.LastTradeTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PriceUpdateResult.Failure(asset.InstrumentId, asset.Name, exception.Message);
        }
    }
}
