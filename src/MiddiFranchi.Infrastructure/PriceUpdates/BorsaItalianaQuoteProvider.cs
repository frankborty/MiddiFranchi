using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MiddiFranchi.Application.Financial;

namespace MiddiFranchi.Infrastructure.PriceUpdates;

public sealed class BorsaItalianaQuoteProvider : IInvestmentQuoteProvider, IDisposable
{
    private const string WarmupUrl =
        "https://grafici.borsaitaliana.it/interactive-chart/IT0003128367-XMIL?lang=it";
    private const string ApiRoot = "https://grafici.borsaitaliana.it/api/instruments";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";

    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly Regex TokenRegex = new("token=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex IsinRegex = new("^[A-Z]{2}[A-Z0-9]{9}[0-9]$", RegexOptions.Compiled);
    private static readonly Regex ExchangeRegex = new("^[A-Z0-9]{4}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly SemaphoreSlim _throttle = new(1, 1);
    private string? _token;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public BorsaItalianaQuoteProvider()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli,
        };

        _http = new HttpClient(handler) { Timeout = RequestTimeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    }

    public async Task<InvestmentQuote> GetQuoteAsync(
        string isin,
        string exchange,
        CancellationToken cancellationToken = default)
    {
        isin = isin.Trim().ToUpperInvariant();
        exchange = exchange.Trim().ToUpperInvariant();

        if (!IsinRegex.IsMatch(isin))
        {
            throw new ArgumentException(
                $"ISIN non valido: '{isin}'. Formato atteso: 2 lettere + 9 caratteri alfanumerici + 1 cifra.",
                nameof(isin));
        }

        if (!ExchangeRegex.IsMatch(exchange))
        {
            throw new ArgumentException(
                $"Mercato non valido: '{exchange}'. Esempi: XMIL, ETLX.",
                nameof(exchange));
        }

        var data = await DownloadHistoryAsync(isin, exchange, cancellationToken)
            .ConfigureAwait(false);
        var points = data.History?.Points;

        if (string.IsNullOrEmpty(data.Transco?.Code))
        {
            throw new InvalidOperationException(
                $"ISIN '{isin}' non riconosciuto dall'API di Borsa Italiana (mercato {exchange}).");
        }

        if (points is null || points.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nessun dato disponibile per '{isin}' (mercato {exchange}).");
        }

        var last = points[^1];
        var price = last.LastPx ?? last.ClosePx;
        if (price is null)
        {
            throw new InvalidOperationException(
                $"Prezzo non disponibile per '{isin}' (mercato {exchange}).");
        }

        var currency = string.IsNullOrEmpty(data.History?.Currency)
            ? "EUR"
            : data.History!.Currency!;
        var lastTradeTime = await TryGetLastTradeTimeAsync(isin, exchange, cancellationToken)
            .ConfigureAwait(false);

        return new InvestmentQuote(
            Isin: isin,
            Price: price.Value,
            Currency: currency,
            AsOf: ParseDate(last.Date),
            Source: "Borsa Italiana",
            LastTradeTime: lastTradeTime);
    }

    /// <summary>
    /// L'endpoint giornaliero non espone l'orario: lo ricaviamo dai punti intraday, senza
    /// rendere fallibile l'aggiornamento se il servizio non li restituisce.
    /// </summary>
    private async Task<TimeOnly?> TryGetLastTradeTimeAsync(
        string isin,
        string exchange,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{ApiRoot}/{isin},{exchange},ISIN/intraday";
            using var response = await SendAsync(
                () => new HttpRequestMessage(HttpMethod.Get, url),
                authenticated: true,
                cancellationToken).ConfigureAwait(false);

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var data = await JsonSerializer
                .DeserializeAsync<IntradayResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return LastTradeTime(data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static TimeOnly? LastTradeTime(IntradayResponse? data)
    {
        var points = data?.Points;
        if (points is null || points.Count == 0)
        {
            return null;
        }

        for (var index = points.Count - 1; index >= 0; index--)
        {
            var point = points[index];

            if (!string.IsNullOrWhiteSpace(point.EndTime) &&
                TimeOnly.TryParseExact(
                    point.EndTime,
                    ["HH:mm:ss", "HH:mm"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var endTime))
            {
                return endTime;
            }

            if (!string.IsNullOrWhiteSpace(point.Time) &&
                DateTime.TryParseExact(
                    point.Time,
                    ["yyyyMMdd-HH:mm:ss", "yyyyMMdd-HH:mm"],
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var bucket))
            {
                return TimeOnly.FromDateTime(bucket);
            }
        }

        return null;
    }

    private async Task<HistoryResponse> DownloadHistoryAsync(
        string isin,
        string exchange,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiRoot}/{isin},{exchange},ISIN/history/period?period=1M&add-last-price=true";

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, url),
            authenticated: true,
            cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var data = await JsonSerializer
            .DeserializeAsync<HistoryResponse>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return data ?? throw new InvalidOperationException(
            "Risposta non valida dall'API di Borsa Italiana.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var request = requestFactory();

            if (authenticated)
            {
                var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            try
            {
                var response = await SendThrottledAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (authenticated &&
                    response.StatusCode == HttpStatusCode.Unauthorized &&
                    attempt < 3)
                {
                    response.Dispose();
                    _token = null;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    response.Dispose();
                    throw new InvalidOperationException(
                        "Accesso bloccato dal WAF di Borsa Italiana (HTTP 403). Riprova più tardi.");
                }

                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    lastError = new InvalidOperationException(
                        $"HTTP {(int)response.StatusCode} da Borsa Italiana (tentativo {attempt}/3).");
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
            }

            if (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            "Impossibile contattare Borsa Italiana dopo 3 tentativi.",
            lastError);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached)
        {
            return cached;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } alreadySet)
            {
                return alreadySet;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, WarmupUrl);
            using var response = await SendThrottledAsync(request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var html = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            var match = TokenRegex.Match(html);

            if (!match.Success)
            {
                throw new InvalidOperationException(
                    "Impossibile estrarre il token JWT dalla pagina interactive-chart di Borsa Italiana.");
            }

            _token = match.Groups[1].Value;
            return _token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendThrottledAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestUtc;
            if (elapsed < MinimumInterval)
            {
                await Task.Delay(MinimumInterval - elapsed, cancellationToken)
                    .ConfigureAwait(false);
            }

            var response = await _http.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            _lastRequestUtc = DateTime.UtcNow;
            return response;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(
            value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    public void Dispose()
    {
        _http.Dispose();
        _tokenLock.Dispose();
        _throttle.Dispose();
    }

    private sealed class HistoryResponse
    {
        [JsonPropertyName("transco")]
        public Transco? Transco { get; set; }

        [JsonPropertyName("history")]
        public HistoryData? History { get; set; }
    }

    private sealed class Transco
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("exchCode")]
        public string? ExchCode { get; set; }
    }

    private sealed class HistoryData
    {
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("historyDt")]
        public List<HistoryPoint>? Points { get; set; }
    }

    private sealed class HistoryPoint
    {
        [JsonPropertyName("dt")]
        public string? Date { get; set; }

        [JsonPropertyName("closePx")]
        public decimal? ClosePx { get; set; }

        [JsonPropertyName("lastPx")]
        public decimal? LastPx { get; set; }
    }

    private sealed class IntradayResponse
    {
        [JsonPropertyName("intradayPoint")]
        public List<IntradayPoint>? Points { get; set; }
    }

    private sealed class IntradayPoint
    {
        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("endTime")]
        public string? EndTime { get; set; }
    }
}
