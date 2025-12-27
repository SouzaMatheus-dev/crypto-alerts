namespace CryptoAlerts.Worker.Domain;

public sealed class AlertRuleConfig
{
    public string Symbol { get; set; } = "BTCUSDT";
    public IReadOnlyList<string> Symbols { get; set; } = Array.Empty<string>();
    public string Timeframe { get; set; } = "1h";
    public int RsiPeriod { get; set; } = 14;
    public decimal BuyRsiThreshold { get; set; } = 30m;
    public decimal SellRsiThreshold { get; set; } = 70m;
    public decimal DcaDropPercent { get; set; } = 3.0m;
    
    public IReadOnlyList<string> GetSymbolsToMonitor()
    {
        if (Symbols.Count > 0)
            return Symbols;
        
        if (!string.IsNullOrWhiteSpace(Symbol))
            return new[] { Symbol };
        
        return new[] { "BTCUSDT" };
    }
}
