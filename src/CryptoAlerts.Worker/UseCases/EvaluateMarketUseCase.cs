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
        var symbol = _cfg.GetSymbolsToMonitor().First();
        return await ExecuteForSymbolAsync(symbol, ct);
    }

    public async Task<AlertDecision> ExecuteForSymbolAsync(string symbol, CancellationToken ct)
    {
        var klines = await _marketData.GetKlinesAsync(symbol, _cfg.Timeframe, limit: 200, ct);

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
                $"ALERTA COMPRA {symbol}",
                $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | Queda do topo recente: {dropPct:F2}% (Topo {recentHigh})"
            );
        }

        if (rsi >= _cfg.SellRsiThreshold)
        {
            return new AlertDecision(
                AlertAction.ConsiderSell,
                $"ALERTA VENDA {symbol}",
                $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | (Aviso: não executa ordem, só sinal)"
            );
        }

        return new AlertDecision(
            AlertAction.Hold,
            $"OK {symbol}",
            $"Preço: {last} | RSI({_cfg.RsiPeriod}): {rsi} | Topo recente: {recentHigh}"
        );
    }

    public async Task<ConsolidatedAlertResult> ExecuteMultipleAsync(CancellationToken ct)
    {
        var symbols = _cfg.GetSymbolsToMonitor();
        var results = new List<MarketAnalysisResult>();
        var alerts = new List<MarketAnalysisResult>();
        var noAlerts = new List<MarketAnalysisResult>();

        foreach (var symbol in symbols)
        {
            try
            {
                var decision = await ExecuteForSymbolAsync(symbol, ct);
                var result = new MarketAnalysisResult(symbol, decision, DateTime.UtcNow);
                
                results.Add(result);

                if (decision.Action is AlertAction.ConsiderBuy or AlertAction.ConsiderSell)
                {
                    alerts.Add(result);
                }
                else
                {
                    noAlerts.Add(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRO ao processar {symbol}: {ex.Message}");
                var errorDecision = new AlertDecision(
                    AlertAction.Info,
                    $"ERRO {symbol}",
                    $"Falha ao analisar: {ex.Message}"
                );
                results.Add(new MarketAnalysisResult(symbol, errorDecision, DateTime.UtcNow));
            }
        }

        return new ConsolidatedAlertResult(results, alerts, noAlerts);
    }
}
