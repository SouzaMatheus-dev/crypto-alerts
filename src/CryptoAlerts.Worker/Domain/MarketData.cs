namespace CryptoAlerts.Worker.Domain;

// Modelo compartilhado para dados de mercado (candles/klines)
public sealed record Kline(
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume
);

// Interface para provedores de dados de mercado
public interface IMarketDataProvider
{
    Task<IReadOnlyList<Kline>> GetKlinesAsync(string symbol, string interval, int limit, CancellationToken ct);
}

