namespace CryptoAlerts.Worker.Infra.CoinGecko;

public sealed class CoinGeckoOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com";
    
    // API key opcional (gratuita, mas com rate limits menores sem key)
    public string? ApiKey { get; set; }
}

