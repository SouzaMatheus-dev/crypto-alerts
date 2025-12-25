using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using CryptoAlerts.Worker.Domain;

namespace CryptoAlerts.Worker.Infra.Binance;

public sealed class BinanceClient : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly BinanceOptions _opt;

    public BinanceClient(HttpClient http, BinanceOptions opt)
    {
        _http = http;
        _opt = opt;

        _http.BaseAddress = new Uri(_opt.BaseUrl);
        if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
            _http.DefaultRequestHeaders.Add("X-MBX-APIKEY", _opt.ApiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<Kline>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct)
    {
        var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
        
        using var resp = await _http.GetAsync(url, ct);
        
        if (!resp.IsSuccessStatusCode && (int)resp.StatusCode == 451)
        {
            var originalBaseUrl = _http.BaseAddress?.ToString();
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            
            var fallbackUrl = "https://data.binance.com";
            
            if (originalBaseUrl?.Contains("data.binance.com") == true)
            {
                throw new HttpRequestException(
                    $"Binance API retornou 451 mesmo com endpoint alternativo. " +
                    $"URL: {originalBaseUrl}{url}. " +
                    $"Resposta: {errorBody}",
                    null,
                    resp.StatusCode
                );
            }
            
            Console.WriteLine($"Erro 451 detectado. Tentando endpoint alternativo: {fallbackUrl}");
            
            using var fallbackHttp = new HttpClient { Timeout = _http.Timeout };
            fallbackHttp.BaseAddress = new Uri(fallbackUrl);
            fallbackHttp.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            using var fallbackResp = await fallbackHttp.GetAsync(url, ct);
            
            if (!fallbackResp.IsSuccessStatusCode)
            {
                var fallbackErrorBody = await fallbackResp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Falha ao buscar dados da Binance mesmo com endpoint alternativo. " +
                    $"Status: {(int)fallbackResp.StatusCode} {fallbackResp.StatusCode}. " +
                    $"URL: {fallbackUrl}{url}. " +
                    $"Resposta: {fallbackErrorBody}",
                    null,
                    fallbackResp.StatusCode
                );
            }
            
            var fallbackRaw = await fallbackResp.Content.ReadAsStringAsync(ct);
            return ParseKlines(fallbackRaw);
        }
        
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            var statusCode = (int)resp.StatusCode;
            
            throw new HttpRequestException(
                $"Falha ao buscar dados da Binance. Status: {statusCode} {resp.StatusCode}. " +
                $"URL: {_http.BaseAddress}{url}. " +
                $"Resposta: {errorBody}",
                null,
                resp.StatusCode
            );
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseKlines(raw);
    }
    
    private static IReadOnlyList<Kline> ParseKlines(string raw)
    {
        var doc = System.Text.Json.JsonDocument.Parse(raw);
        var list = new List<Kline>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var openTimeMs = item[0].GetInt64();
            var open = ParseDec(item[1].GetString());
            var high = ParseDec(item[2].GetString());
            var low = ParseDec(item[3].GetString());
            var close = ParseDec(item[4].GetString());
            var vol = ParseDec(item[5].GetString());

            list.Add(new Kline(
                DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs),
                open, high, low, close, vol
            ));
        }

        return list;

        static decimal ParseDec(string? s) =>
            decimal.Parse(s ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture);
    }
}
