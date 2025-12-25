namespace CryptoAlerts.Worker.Infra.Binance;

public sealed class BinanceOptions
{
    public string BaseUrl { get; set; } = "https://api.binance.com";
    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }
}
