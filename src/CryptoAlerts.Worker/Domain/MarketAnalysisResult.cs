namespace CryptoAlerts.Worker.Domain;

public sealed record MarketAnalysisResult(
    string Symbol,
    AlertDecision Decision,
    DateTime AnalyzedAt
);

public sealed record ConsolidatedAlertResult(
    IReadOnlyList<MarketAnalysisResult> Results,
    IReadOnlyList<MarketAnalysisResult> Alerts,
    IReadOnlyList<MarketAnalysisResult> NoAlerts
)
{
    public bool HasAlerts => Alerts.Count > 0;
    public int TotalAnalyzed => Results.Count;
    public int TotalAlerts => Alerts.Count;
}

