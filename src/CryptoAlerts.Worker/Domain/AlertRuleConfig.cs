namespace CryptoAlerts.Worker.Domain;

public sealed class AlertRuleConfig
{
    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "1h";
    public int RsiPeriod { get; set; } = 14;
    public decimal BuyRsiThreshold { get; set; } = 30m;
    public decimal SellRsiThreshold { get; set; } = 70m;
    public decimal DcaDropPercent { get; set; } = 3.0m; // ex: alerta se cair 3% do último topo recente
}
