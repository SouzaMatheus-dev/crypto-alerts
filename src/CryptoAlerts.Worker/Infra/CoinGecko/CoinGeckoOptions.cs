namespace CryptoAlerts.Worker.Infra.CoinGecko;

public sealed class CoinGeckoOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com";
    public string? ApiKey { get; set; }
}

