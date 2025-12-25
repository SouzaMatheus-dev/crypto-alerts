namespace CryptoAlerts.Worker.UseCases;

public static class Indicators
{
    public static decimal Rsi(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period + 1) return 50m;

        decimal gain = 0m, loss = 0m;

        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) gain += diff;
            else loss += -diff;
        }

        var avgGain = gain / period;
        var avgLoss = loss / period;

        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            var g = diff > 0 ? diff : 0m;
            var l = diff < 0 ? -diff : 0m;

            avgGain = (avgGain * (period - 1) + g) / period;
            avgLoss = (avgLoss * (period - 1) + l) / period;
        }

        if (avgLoss == 0) return 100m;

        var rs = avgGain / avgLoss;
        var rsi = 100m - (100m / (1m + rs));
        return decimal.Round(rsi, 2);
    }

    public static decimal PercentChange(decimal from, decimal to)
    {
        if (from == 0) return 0;
        return ((to - from) / from) * 100m;
    }
}
