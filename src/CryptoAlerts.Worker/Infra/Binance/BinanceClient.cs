using System.Globalization;
using System.Net.Http.Headers;

namespace CryptoAlerts.Worker.Infra.Binance;

public sealed class BinanceClient
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
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadAsStringAsync(ct);

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
