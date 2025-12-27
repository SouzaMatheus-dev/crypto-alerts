using CryptoAlerts.Worker.Domain;
using CryptoAlerts.Worker.Infra.Binance;
using CryptoAlerts.Worker.Infra.CoinGecko;
using CryptoAlerts.Worker.Infra.Email;
using CryptoAlerts.Worker.UseCases;

static string? Env(string key) => Environment.GetEnvironmentVariable(key);

static IReadOnlyList<string> ParseSymbols(string? symbolsEnv)
{
    if (string.IsNullOrWhiteSpace(symbolsEnv))
        return Array.Empty<string>();

    return symbolsEnv
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .ToList();
}

var symbolsEnv = Env("RULES_SYMBOLS");
var symbolEnv = Env("RULES_SYMBOL");
var symbols = ParseSymbols(symbolsEnv);

var cfg = new AlertRuleConfig
{
    Symbol = symbolEnv ?? "BTCUSDT",
    Symbols = symbols.Count > 0 ? symbols : Array.Empty<string>(),
    Timeframe = Env("RULES_TIMEFRAME") ?? "1h",
    RsiPeriod = int.TryParse(Env("RULES_RSI_PERIOD"), out var rp) ? rp : 14,
    BuyRsiThreshold = decimal.TryParse(Env("RULES_BUY_RSI"), out var br) ? br : 30m,
    SellRsiThreshold = decimal.TryParse(Env("RULES_SELL_RSI"), out var sr) ? sr : 70m,
    DcaDropPercent = decimal.TryParse(Env("RULES_DCA_DROP"), out var dd) ? dd : 3.0m
};

var symbolsToMonitor = cfg.GetSymbolsToMonitor();
var isMultiSymbol = symbolsToMonitor.Count > 1;

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
        ApiKey = Env("COINGECKO_APIKEY")
    };
    marketData = new CoinGeckoClient(http, coinGeckoOpt);
    Console.WriteLine("Usando CoinGecko como provedor de dados de mercado (sem bloqueio geográfico)");
}

var useCase = new EvaluateMarketUseCase(marketData, cfg);
var sender = new GmailSmtpEmailSender(emailOpt);

try
{
    if (isMultiSymbol)
    {
        Console.WriteLine($"Monitorando {symbolsToMonitor.Count} criptomoedas: {string.Join(", ", symbolsToMonitor)}");
        var consolidated = await useCase.ExecuteMultipleAsync(CancellationToken.None);

        Console.WriteLine($"Análise concluída: {consolidated.TotalAnalyzed} criptos | {consolidated.TotalAlerts} alertas");

        if (consolidated.HasAlerts)
        {
            var emailMode = Env("EMAIL_MODE")?.ToLowerInvariant() ?? "consolidated";

            if (emailMode == "individual")
            {
                foreach (var alert in consolidated.Alerts)
                {
                    var htmlBody = EmailTemplate.GenerateSingleAlert(alert.Decision);
                    await sender.SendAsync(alert.Decision.Title, htmlBody, isHtml: true, CancellationToken.None);
                    Console.WriteLine($"Email enviado: {alert.Decision.Title}");
                }
            }
            else
            {
                var subject = $"Crypto Alerts - {consolidated.TotalAlerts} Oportunidade(s) Detectada(s)";
                var htmlBody = EmailTemplate.GenerateConsolidatedAlert(consolidated);

                await sender.SendAsync(subject, htmlBody, isHtml: true, CancellationToken.None);
                Console.WriteLine($"Email consolidado enviado: {consolidated.TotalAlerts} alerta(s)");
            }
        }
        else
        {
            Console.WriteLine("Nenhum alerta detectado em todas as criptomoedas monitoradas.");
        }
    }
    else
    {
        var decision = await useCase.ExecuteAsync(CancellationToken.None);

        if (decision.Action is AlertAction.ConsiderBuy or AlertAction.ConsiderSell)
        {
            var htmlBody = EmailTemplate.GenerateSingleAlert(decision);
            await sender.SendAsync(decision.Title, htmlBody, isHtml: true, CancellationToken.None);
            Console.WriteLine($"Email sent: {decision.Title}");
        }
        else
        {
            Console.WriteLine($"No alert. {decision.Title} - {decision.Message}");
        }
    }
}
catch (HttpRequestException httpEx)
{
    var errorMsg = $"Erro ao conectar com a API de dados de mercado: {httpEx.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");

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

    Environment.Exit(1);
}
catch (Exception ex)
{
    var errorMsg = $"Erro inesperado: {ex.Message}";
    Console.WriteLine($"ERRO: {errorMsg}");
    Console.WriteLine(ex.ToString());

    Environment.Exit(1);
}
