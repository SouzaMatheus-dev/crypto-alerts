using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using CryptoAlerts.Worker.Domain;

namespace CryptoAlerts.Worker.Infra.CoinGecko;

public sealed class CoinGeckoClient : IMarketDataProvider
{
    private readonly HttpClient _http;
    private readonly CoinGeckoOptions _opt;

    public CoinGeckoClient(HttpClient http, CoinGeckoOptions opt)
    {
        _http = http;
        _opt = opt;

        _http.BaseAddress = new Uri(_opt.BaseUrl);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // CoinGecko permite rate limit sem key, mas recomenda usar key para limites maiores
        if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
        {
            _http.DefaultRequestHeaders.Add("x-cg-demo-api-key", _opt.ApiKey);
        }
    }

    public async Task<IReadOnlyList<Kline>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct)
    {
        // CoinGecko usa IDs diferentes (ex: "bitcoin" ao invés de "BTCUSDT")
        // Precisamos mapear o symbol para o coin_id
        var coinId = MapSymbolToCoinId(symbol);
        
        // Mapear interval para days (CoinGecko OHLC usa days como parâmetro)
        // O endpoint retorna candles diários, então calculamos quantos dias precisamos
        var days = MapIntervalToDays(interval, limit);
        
        // Endpoint: GET /api/v3/coins/{coin_id}/ohlc?vs_currency=usd&days={days}
        // Retorna: [[timestamp_ms, open, high, low, close], ...] ordenado do mais antigo ao mais recente
        // Nota: CoinGecko OHLC retorna candles diários. Para intervalos menores (1h, 4h), 
        // usamos candles diários e fazemos aproximação (útil para RSI e análise de tendência)
        var url = $"/api/v3/coins/{coinId}/ohlc?vs_currency=usd&days={days}";
        
        using var resp = await _http.GetAsync(url, ct);
        
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            var statusCode = (int)resp.StatusCode;
            
            throw new HttpRequestException(
                $"Falha ao buscar dados do CoinGecko. Status: {statusCode} {resp.StatusCode}. " +
                $"URL: {_http.BaseAddress}{url}. " +
                $"Resposta: {errorBody}",
                null,
                resp.StatusCode
            );
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseKlines(raw, limit);
    }

    private static IReadOnlyList<Kline> ParseKlines(string raw, int limit)
    {
        var doc = System.Text.Json.JsonDocument.Parse(raw);
        var list = new List<Kline>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            // CoinGecko retorna: [timestamp_ms, open, high, low, close]
            var timestampMs = item[0].GetInt64();
            var open = ParseDec(item[1].GetString());
            var high = ParseDec(item[2].GetString());
            var low = ParseDec(item[3].GetString());
            var close = ParseDec(item[4].GetString());

            // CoinGecko não retorna volume no OHLC, então usamos 0
            // Se precisar de volume, pode usar outro endpoint
            list.Add(new Kline(
                DateTimeOffset.FromUnixTimeMilliseconds(timestampMs),
                open, high, low, close, 0m
            ));
        }

        // Retorna os últimos N candles (CoinGecko retorna ordenado do mais antigo ao mais recente)
        return list.TakeLast(limit).ToList();

        static decimal ParseDec(string? s) =>
            decimal.Parse(s ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    // Mapeia símbolos comuns para coin_id do CoinGecko
    private static string MapSymbolToCoinId(string symbol)
    {
        // Remove "USDT", "USD", etc. e converte para lowercase
        var baseSymbol = symbol
            .Replace("USDT", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("BTC", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        // Mapeamento comum
        return baseSymbol switch
        {
            "btc" => "bitcoin",
            "eth" => "ethereum",
            "bnb" => "binancecoin",
            "sol" => "solana",
            "ada" => "cardano",
            "xrp" => "ripple",
            "dot" => "polkadot",
            "doge" => "dogecoin",
            "matic" => "matic-network",
            "avax" => "avalanche-2",
            "link" => "chainlink",
            "ltc" => "litecoin",
            "bch" => "bitcoin-cash",
            "xlm" => "stellar",
            "atom" => "cosmos",
            "algo" => "algorand",
            "vet" => "vechain",
            "icp" => "internet-computer",
            "fil" => "filecoin",
            "trx" => "tron",
            "etc" => "ethereum-classic",
            "xmr" => "monero",
            "eos" => "eos",
            "aave" => "aave",
            "uni" => "uniswap",
            "cake" => "pancakeswap-token",
            _ => baseSymbol // Tenta usar o símbolo direto (pode não funcionar)
        };
    }

    // Mapeia intervalos para days (CoinGecko OHLC retorna candles diários)
    // Para intervalos menores (1h, 4h), usamos candles diários como aproximação
    private static int MapIntervalToDays(string interval, int limit)
    {
        // Extrai número e unidade (ex: "1h" -> 1, "h")
        var match = System.Text.RegularExpressions.Regex.Match(interval, @"^(\d+)([mhdwMy])$");
        if (!match.Success)
            return Math.Max(30, limit); // default: pelo menos 30 dias ou o limit

        var value = int.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLowerInvariant();

        // CoinGecko OHLC retorna candles diários
        // Para intervalos menores, precisamos de mais dias para ter dados suficientes
        // Exemplo: para 200 candles de 1h, precisamos de ~8-10 dias de dados diários
        var daysNeeded = unit switch
        {
            "m" => Math.Max(limit, 30), // minutos: usa pelo menos 30 dias
            "h" => Math.Max(limit / 24 + 1, 30), // horas: converte para dias com margem
            "d" => Math.Max(limit, 30), // dias: usa o limit ou 30, o que for maior
            "w" => Math.Max(limit * 7, 30), // semanas: converte para dias
            _ => Math.Max(limit, 30) // default
        };

        // CoinGecko permite até 90 dias no plano gratuito, 365 no pago
        // Retorna o mínimo necessário com margem de segurança
        return Math.Min(Math.Max(daysNeeded, 30), 90);
    }
}

