using CryptoAlerts.Worker.Domain;
using CryptoAlerts.Worker.Infra.Binance;
using CryptoAlerts.Worker.Infra.CoinGecko;
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
    Environment.Exit(1);
    return;
}

// Escolhe o provedor de dados de mercado (CoinGecko é o padrão, sem bloqueio geográfico)
IMarketDataProvider marketData;
var provider = Env("MARKET_DATA_PROVIDER")?.ToLowerInvariant() ?? "coingecko";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

if (provider == "binance")
{
    var binanceOpt = new BinanceOptions
    {
        BaseUrl = Env("BINANCE_BASEURL") ?? "https://api.binance.com",
        ApiKey = Env("BINANCE_APIKEY"),
        SecretKey = Env("BINANCE_SECRETKEY")
    };
    marketData = new BinanceClient(http, binanceOpt);
    Console.WriteLine("Usando Binance como provedor de dados de mercado");
}
else
{
    var coinGeckoOpt = new CoinGeckoOptions
    {
        BaseUrl = Env("COINGECKO_BASEURL") ?? "https://api.coingecko.com",
        ApiKey = Env("COINGECKO_APIKEY") // opcional, mas recomendado para rate limits maiores
    };
    marketData = new CoinGeckoClient(http, coinGeckoOpt);
    Console.WriteLine("Usando CoinGecko como provedor de dados de mercado (sem bloqueio geográfico)");
}

var useCase = new EvaluateMarketUseCase(marketData, cfg);
var sender = new GmailSmtpEmailSender(emailOpt);

try
{
    var decision = await useCase.ExecuteAsync(CancellationToken.None);

    // TESTE: Envia email sempre (mesmo sem alerta) para testar no GitHub Actions
    // TODO: Reverter depois do teste para enviar apenas quando houver alerta
    await sender.SendAsync(decision.Title, decision.Message, CancellationToken.None);
    Console.WriteLine($"Email sent: {decision.Title} - {decision.Message}");

    // Código original (comentado para teste):
    // if (decision.Action is AlertAction.ConsiderBuy or AlertAction.ConsiderSell)
    // {
    //     await sender.SendAsync(decision.Title, decision.Message, CancellationToken.None);
    //     Console.WriteLine($"Email sent: {decision.Title}");
    // }
    // else
    // {
    //     Console.WriteLine($"No alert. {decision.Title} - {decision.Message}");
    // }
}
catch (HttpRequestException httpEx)
{
    var errorMsg = $"Erro ao conectar com a API de dados de mercado: {httpEx.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");

    // Se for erro 451 (bloqueio geográfico da Binance), fornece dicas
    if (httpEx.Message.Contains("451") || httpEx.Message.Contains("Unavailable For Legal Reasons"))
    {
        Console.WriteLine();
        Console.WriteLine("DICA: O erro 451 geralmente indica bloqueio geográfico da Binance.");
        Console.WriteLine("Soluções possíveis:");
        Console.WriteLine("  1. Use CoinGecko como provedor: MARKET_DATA_PROVIDER=coingecko");
        Console.WriteLine("  2. Use uma URL alternativa da Binance (ex: api.binance.us)");
        Console.WriteLine("  3. Execute o worker em uma região não bloqueada");
        Console.WriteLine();
    }

    // Opcional: enviar e-mail de erro
    // await sender.SendAsync("Erro no Crypto Alerts", errorMsg, CancellationToken.None);

    // Retorna código de erro para falhar o workflow
    Environment.Exit(1);
}
catch (Exception ex)
{
    var errorMsg = $"Erro inesperado: {ex.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");
    Console.WriteLine(ex.ToString());

    // Opcional: enviar e-mail de erro
    // await sender.SendAsync("Erro no Crypto Alerts", errorMsg, CancellationToken.None);

    // Retorna código de erro para falhar o workflow
    Environment.Exit(1);
}
