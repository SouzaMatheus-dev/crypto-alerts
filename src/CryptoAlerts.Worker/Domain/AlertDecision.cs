namespace CryptoAlerts.Worker.Domain;

public enum AlertAction
{
    Hold = 0,
    ConsiderBuy = 1,
    ConsiderSell = 2,
    Info = 3
}

public sealed record AlertDecision(
    AlertAction Action,
    string Title,
    string Message
);
