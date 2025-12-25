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

    if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
    {
      _http.DefaultRequestHeaders.Add("x-cg-demo-api-key", _opt.ApiKey);
    }
  }

  public async Task<IReadOnlyList<Kline>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct)
  {
    var coinId = MapSymbolToCoinId(symbol);

    if (string.IsNullOrWhiteSpace(coinId))
    {
      throw new ArgumentException($"Não foi possível mapear o símbolo '{symbol}' para um coin_id do CoinGecko.");
    }

    var days = MapIntervalToDays(interval, limit);
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
      var timestampMs = item[0].GetInt64();
      var open = item[1].GetDecimal();
      var high = item[2].GetDecimal();
      var low = item[3].GetDecimal();
      var close = item[4].GetDecimal();

      list.Add(new Kline(
          DateTimeOffset.FromUnixTimeMilliseconds(timestampMs),
          open, high, low, close, 0m
      ));
    }

    return list.TakeLast(limit).ToList();
  }

  private static string MapSymbolToCoinId(string symbol)
  {
    var symbolLower = symbol.ToLowerInvariant();

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

    var baseSymbol = symbol
        .Replace("USDT", "", StringComparison.OrdinalIgnoreCase)
        .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
        .ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(baseSymbol))
    {
      throw new ArgumentException($"Não foi possível mapear o símbolo '{symbol}' para um coin_id do CoinGecko. " +
          "Configure um símbolo válido (ex: BTCUSDT, ETHUSDT) ou adicione o mapeamento em MapSymbolToCoinId.");
    }

    return baseSymbol;
  }

  private static int MapIntervalToDays(string interval, int limit)
  {
    var match = System.Text.RegularExpressions.Regex.Match(interval, @"^(\d+)([mhdwMy])$");
    if (!match.Success)
      return Math.Max(30, limit);

    var value = int.Parse(match.Groups[1].Value);
    var unit = match.Groups[2].Value.ToLowerInvariant();

    var daysNeeded = unit switch
    {
      "m" => Math.Max(limit, 30),
      "h" => Math.Max(limit / 24 + 1, 30),
      "d" => Math.Max(limit, 30),
      "w" => Math.Max(limit * 7, 30),
      _ => Math.Max(limit, 30)
    };

    return Math.Min(Math.Max(daysNeeded, 30), 90);
  }
}

