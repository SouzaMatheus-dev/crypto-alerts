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
        // Endpoint público:
        // GET /api/v3/klines?symbol=BTCUSDT&interval=1h&limit=200
        var url = $"/api/v3/klines?symbol={symbol}&interval={interval}&limit={limit}";
        
        // Tenta primeiro com a URL configurada
        using var resp = await _http.GetAsync(url, ct);
        
        // Se receber erro 451, tenta automaticamente com o endpoint alternativo data.binance.com
        if (!resp.IsSuccessStatusCode && (int)resp.StatusCode == 451)
        {
            var originalBaseUrl = _http.BaseAddress?.ToString();
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            
            // Endpoint alternativo para dados públicos (não retorna 451)
            var fallbackUrl = "https://data.binance.com";
            
            // Se já está usando o fallback, não tenta novamente
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
            
            // Cria um novo HttpClient para o endpoint alternativo
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
            
            var raw = await fallbackResp.Content.ReadAsStringAsync(ct);
            return ParseKlines(raw);
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
        // parse simples sem dependências externas
        // usamos System.Text.Json para ler como JsonElement
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
