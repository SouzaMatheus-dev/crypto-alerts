using CryptoAlerts.Worker.Domain;
using CryptoAlerts.Worker.Infra.Binance;

namespace CryptoAlerts.Worker.UseCases;

public sealed class EvaluateMarketUseCase
{
    private readonly BinanceClient _binance;
    private readonly AlertRuleConfig _cfg;

    public EvaluateMarketUseCase(BinanceClient binance, AlertRuleConfig cfg)
    {
        _binance = binance;
        _cfg = cfg;
    }

    public async Task<AlertDecision> ExecuteAsync(CancellationToken ct)
    {
        var klines = await _binance.GetKlinesAsync(_cfg.Symbol, _cfg.Timeframe, limit: 200, ct);

        var closes = klines.Select(k => k.Close).ToList();
        var last = closes[^1];

        var rsi = Indicators.Rsi(closes, _cfg.RsiPeriod);

        // DCA drop simples: compara contra o maior close dos últimos 48 candles
        var window = closes.TakeLast(Math.Min(48, closes.Count)).ToList();
        var recentHigh = window.Max();
        var dropPct = Indicators.PercentChange(recentHigh, last); // negativo quando caiu
        var dropAbs = Math.Abs(dropPct);

        // Regras MVP:
        // - Comprar: RSI <= BuyThreshold OU queda >= DcaDropPercent
        // - Vender: RSI >= SellThreshold (apenas aviso; você decide)
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
