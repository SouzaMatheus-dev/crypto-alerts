namespace CryptoAlerts.Worker.Infra.Binance;

public sealed class BinanceOptions
{
    public string BaseUrl { get; set; } = "https://api.binance.com";

    // Read-only keys (opcional para endpoints públicos; mas deixo pronto)
    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }
}
