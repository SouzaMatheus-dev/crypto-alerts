using CryptoAlerts.Worker.Domain;
using CryptoAlerts.Worker.Infra.Binance;
using CryptoAlerts.Worker.Infra.Email;
using CryptoAlerts.Worker.UseCases;

static string? Env(string key) => Environment.GetEnvironmentVariable(key);

var cfg = new AlertRuleConfig
{
    Symbol = Env("RULES_SYMBOL") ?? "BTCUSDT",
    Timeframe = Env("RULES_TIMEFRAME") ?? "1h",
    RsiPeriod = int.TryParse(Env("RULES_RSI_PERIOD"), out var rp) ? rp : 14,
    BuyRsiThreshold = decimal.TryParse(Env("RULES_BUY_RSI"), out var br) ? br : 30m,
    SellRsiThreshold = decimal.TryParse(Env("RULES_SELL_RSI"), out var sr) ? sr : 70m,
    DcaDropPercent = decimal.TryParse(Env("RULES_DCA_DROP"), out var dd) ? dd : 3.0m
};

var binanceOpt = new BinanceOptions
{
    BaseUrl = Env("BINANCE_BASEURL") ?? "https://api.binance.com",
    ApiKey = Env("BINANCE_APIKEY"),      // opcional
    SecretKey = Env("BINANCE_SECRETKEY") // opcional
};

var emailOpt = new EmailOptions
{
    FromEmail = Env("GMAIL_FROM") ?? "",
    ToEmail = Env("GMAIL_TO") ?? "",
    AppPassword = Env("GMAIL_APP_PASSWORD") ?? "",
};

if (string.IsNullOrWhiteSpace(emailOpt.FromEmail) ||
    string.IsNullOrWhiteSpace(emailOpt.ToEmail) ||
    string.IsNullOrWhiteSpace(emailOpt.AppPassword))
{
    Console.WriteLine("Missing Gmail env vars. Set GMAIL_FROM, GMAIL_TO, GMAIL_APP_PASSWORD.");
    return;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
var binance = new BinanceClient(http, binanceOpt);

var useCase = new EvaluateMarketUseCase(binance, cfg);
var sender = new GmailSmtpEmailSender(emailOpt);

try
{
    var decision = await useCase.ExecuteAsync(CancellationToken.None);

    // manda e-mail só quando for BUY/SELL; se quiser diário/healthcheck, ajusta
    if (decision.Action is AlertAction.ConsiderBuy or AlertAction.ConsiderSell)
    {
        await sender.SendAsync(decision.Title, decision.Message, CancellationToken.None);
        Console.WriteLine($"Email sent: {decision.Title}");
    }
    else
    {
        Console.WriteLine($"No alert. {decision.Title} - {decision.Message}");
    }
}
catch (HttpRequestException httpEx)
{
    var errorMsg = $"Erro ao conectar com a API da Binance: {httpEx.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");
    
    // Se for erro 451 (bloqueio geográfico), fornece dicas
    if (httpEx.Message.Contains("451") || httpEx.Message.Contains("Unavailable For Legal Reasons"))
    {
        Console.WriteLine();
        Console.WriteLine("DICA: O erro 451 geralmente indica bloqueio geográfico.");
        Console.WriteLine("Soluções possíveis:");
        Console.WriteLine("  1. Use uma URL alternativa da Binance (ex: api.binance.us)");
        Console.WriteLine("  2. Execute o worker em uma região não bloqueada");
        Console.WriteLine("  3. Configure um proxy se necessário");
        Console.WriteLine();
    }
    
    // Opcional: enviar e-mail de erro
    // await sender.SendAsync("Erro no Crypto Alerts", errorMsg, CancellationToken.None);
}
catch (Exception ex)
{
    var errorMsg = $"Erro inesperado: {ex.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");
    Console.WriteLine(ex.ToString());
    
    // Opcional: enviar e-mail de erro
    // await sender.SendAsync("Erro no Crypto Alerts", errorMsg, CancellationToken.None);
}
