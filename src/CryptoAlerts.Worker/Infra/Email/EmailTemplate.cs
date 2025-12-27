using CryptoAlerts.Worker.Domain;

namespace CryptoAlerts.Worker.Infra.Email;

public static class EmailTemplate
{
    public static string GenerateSingleAlert(AlertDecision decision)
    {
        var actionColor = decision.Action switch
        {
            AlertAction.ConsiderBuy => "#10b981",
            AlertAction.ConsiderSell => "#ef4444",
            _ => "#6b7280"
        };

        var actionLabel = decision.Action switch
        {
            AlertAction.ConsiderBuy => "COMPRA",
            AlertAction.ConsiderSell => "VENDA",
            _ => "HOLD"
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{decision.Title}</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td style=""padding: 40px 20px; text-align: center; background-color: #ffffff;"">
                <table role=""presentation"" style=""max-width: 600px; margin: 0 auto; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 30px; text-align: center; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px 8px 0 0;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;"">Crypto Alerts</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 30px;"">
                            <div style=""display: inline-block; padding: 8px 16px; background-color: {actionColor}; color: #ffffff; border-radius: 4px; font-weight: 600; font-size: 14px; text-transform: uppercase; margin-bottom: 20px;"">
                                {actionLabel}
                            </div>
                            <h2 style=""margin: 0 0 20px 0; color: #111827; font-size: 20px; font-weight: 600;"">{decision.Title}</h2>
                            <p style=""margin: 0; color: #6b7280; font-size: 16px; line-height: 1.6;"">{decision.Message.Replace("\n", "<br>")}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 30px; text-align: center; border-top: 1px solid #e5e7eb; background-color: #f9fafb; border-radius: 0 0 8px 8px;"">
                            <p style=""margin: 0; color: #9ca3af; font-size: 12px;"">
                                Este é um alerta automático. Sempre faça sua própria pesquisa antes de investir.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public static string GenerateConsolidatedAlert(ConsolidatedAlertResult consolidated)
    {
        var alertsHtml = string.Join("", consolidated.Alerts.Select(alert =>
        {
            var actionColor = alert.Decision.Action switch
            {
                AlertAction.ConsiderBuy => "#10b981",
                AlertAction.ConsiderSell => "#ef4444",
                _ => "#6b7280"
            };

            var actionLabel = alert.Decision.Action switch
            {
                AlertAction.ConsiderBuy => "COMPRA",
                AlertAction.ConsiderSell => "VENDA",
                _ => "HOLD"
            };

            return $@"
                    <tr>
                        <td style=""padding: 20px; background-color: #ffffff; border-left: 4px solid {actionColor}; border-radius: 4px; margin-bottom: 16px;"">
                            <div style=""display: inline-block; padding: 6px 12px; background-color: {actionColor}; color: #ffffff; border-radius: 4px; font-weight: 600; font-size: 12px; text-transform: uppercase; margin-bottom: 12px;"">
                                {actionLabel}
                            </div>
                            <h3 style=""margin: 0 0 8px 0; color: #111827; font-size: 18px; font-weight: 600;"">{alert.Decision.Title}</h3>
                            <p style=""margin: 0; color: #6b7280; font-size: 14px; line-height: 1.6;"">{alert.Decision.Message.Replace("\n", "<br>")}</p>
                        </td>
                    </tr>";
        }));

        var noAlertsHtml = consolidated.NoAlerts.Count > 0
            ? $@"
                    <tr>
                        <td style=""padding: 20px 0 0 0;"">
                            <h3 style=""margin: 0 0 16px 0; color: #6b7280; font-size: 16px; font-weight: 600;"">Sem Alertas</h3>
                        </td>
                    </tr>
                    {string.Join("", consolidated.NoAlerts.Select(noAlert =>
                        $@"
                    <tr>
                        <td style=""padding: 12px 20px; background-color: #f9fafb; border-radius: 4px; margin-bottom: 8px;"">
                            <p style=""margin: 0; color: #9ca3af; font-size: 14px;"">{noAlert.Decision.Message}</p>
                        </td>
                    </tr>"
                    ))}"
            : "";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Crypto Alerts - {consolidated.TotalAlerts} Oportunidade(s)</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td style=""padding: 40px 20px; text-align: center; background-color: #f3f4f6;"">
                <table role=""presentation"" style=""max-width: 600px; margin: 0 auto; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 30px; text-align: center; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px 8px 0 0;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 600;"">Crypto Alerts</h1>
                            <p style=""margin: 8px 0 0 0; color: #e0e7ff; font-size: 14px;"">{consolidated.TotalAnalyzed} criptomoedas analisadas | {consolidated.TotalAlerts} oportunidade(s) detectada(s)</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 30px;"">
                            <h2 style=""margin: 0 0 20px 0; color: #111827; font-size: 20px; font-weight: 600;"">Alertas</h2>
                            <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
{alertsHtml}
                            </table>
                            {noAlertsHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 30px; text-align: center; border-top: 1px solid #e5e7eb; background-color: #f9fafb; border-radius: 0 0 8px 8px;"">
                            <p style=""margin: 0; color: #9ca3af; font-size: 12px;"">
                                Análise realizada em {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
                            </p>
                            <p style=""margin: 8px 0 0 0; color: #9ca3af; font-size: 12px;"">
                                Este é um alerta automático. Sempre faça sua própria pesquisa antes de investir.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}

