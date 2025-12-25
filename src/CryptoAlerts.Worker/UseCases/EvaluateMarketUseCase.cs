using CryptoAlerts.Worker.Domain;

namespace CryptoAlerts.Worker.UseCases;

public sealed class EvaluateMarketUseCase
{
    private readonly IMarketDataProvider _marketData;
    private readonly AlertRuleConfig _cfg;

    public EvaluateMarketUseCase(IMarketDataProvider marketData, AlertRuleConfig cfg)
    {
        _marketData = marketData;
        _cfg = cfg;
    }

    public async Task<AlertDecision> ExecuteAsync(CancellationToken ct)
    {
        var klines = await _marketData.GetKlinesAsync(_cfg.Symbol, _cfg.Timeframe, limit: 200, ct);

        var closes = klines.Select(k => k.Close).ToList();
        var last = closes[^1];

        var rsi = Indicators.Rsi(closes, _cfg.RsiPeriod);

        var window = closes.TakeLast(Math.Min(48, closes.Count)).ToList();
        var recentHigh = window.Max();
        var dropPct = Indicators.PercentChange(recentHigh, last);
        var dropAbs = Math.Abs(dropPct);

        if (rsi <= _cfg.BuyRsiThreshold || (dropPct < 0 && dropAbs >= _cfg.DcaDropPercent))
        {
            return new AlertDecision(
                AlertAction.ConsiderBuy,
                $"ALERTA COMPRA {_cfg.Symbol}",
                $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | Queda do topo recente: {dropPct:F2}% (Topo {recentHigh})"
            );
        }

        if (rsi >= _cfg.SellRsiThreshold)
        {
            return new AlertDecision(
                AlertAction.ConsiderSell,
                $"ALERTA VENDA {_cfg.Symbol}",
                $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | (Aviso: não executa ordem, só sinal)"
            );
        }

        return new AlertDecision(
            AlertAction.Hold,
            $"OK {_cfg.Symbol}",
            $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | Topo recente: {recentHigh}"
        );
    }
}
