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
        
        if (string.IsNullOrWhiteSpace(coinId))
        {
            throw new ArgumentException($"Não foi possível mapear o símbolo '{symbol}' para um coin_id do CoinGecko.");
        }
        
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
            var fullUrl = $"{_http.BaseAddress}{url}";
            
            throw new HttpRequestException(
                $"Falha ao buscar dados do CoinGecko. Status: {statusCode} {resp.StatusCode}. " +
                $"URL: {fullUrl}. " +
                $"Símbolo: {symbol} -> Coin ID: {coinId}. " +
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
        // Primeiro tenta mapear o símbolo completo (ex: "BTCUSDT" -> "bitcoin")
        var symbolLower = symbol.ToLowerInvariant();
        
        // Mapeamento direto de símbolos completos
        if (symbolLower.Contains("btc"))
            return "bitcoin";
        if (symbolLower.Contains("eth"))
            return "ethereum";
        if (symbolLower.Contains("bnb"))
            return "binancecoin";
        if (symbolLower.Contains("sol"))
            return "solana";
        if (symbolLower.Contains("ada"))
            return "cardano";
        if (symbolLower.Contains("xrp"))
            return "ripple";
        if (symbolLower.Contains("dot"))
            return "polkadot";
        if (symbolLower.Contains("doge"))
            return "dogecoin";
        if (symbolLower.Contains("matic"))
            return "matic-network";
        if (symbolLower.Contains("avax"))
            return "avalanche-2";
        if (symbolLower.Contains("link"))
            return "chainlink";
        if (symbolLower.Contains("ltc"))
            return "litecoin";
        if (symbolLower.Contains("bch"))
            return "bitcoin-cash";
        if (symbolLower.Contains("xlm"))
            return "stellar";
        if (symbolLower.Contains("atom"))
            return "cosmos";
        if (symbolLower.Contains("algo"))
            return "algorand";
        if (symbolLower.Contains("vet"))
            return "vechain";
        if (symbolLower.Contains("icp"))
            return "internet-computer";
        if (symbolLower.Contains("fil"))
            return "filecoin";
        if (symbolLower.Contains("trx"))
            return "tron";
        if (symbolLower.Contains("etc"))
            return "ethereum-classic";
        if (symbolLower.Contains("xmr"))
            return "monero";
        if (symbolLower.Contains("eos"))
            return "eos";
        if (symbolLower.Contains("aave"))
            return "aave";
        if (symbolLower.Contains("uni"))
            return "uniswap";
        if (symbolLower.Contains("cake"))
            return "pancakeswap-token";
        
        // Se não encontrou, remove sufixos comuns e tenta novamente
        var baseSymbol = symbol
            .Replace("USDT", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        
        // Retorna o baseSymbol ou lança erro se vazio
        if (string.IsNullOrWhiteSpace(baseSymbol))
        {
            throw new ArgumentException($"Não foi possível mapear o símbolo '{symbol}' para um coin_id do CoinGecko. " +
                "Configure um símbolo válido (ex: BTCUSDT, ETHUSDT) ou adicione o mapeamento em MapSymbolToCoinId.");
        }
        
        return baseSymbol;
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

