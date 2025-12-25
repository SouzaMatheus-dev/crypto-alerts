namespace CryptoAlerts.Worker.Domain;

public sealed record Kline(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);

public interface IMarketDataProvider
{
    Task<IReadOnlyList<Kline>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct);
}

