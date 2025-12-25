namespace CryptoAlerts.Worker.Infra.Binance;

// Klines: [ openTime, open, high, low, close, volume, closeTime, ... ]
public sealed record Kline(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);
