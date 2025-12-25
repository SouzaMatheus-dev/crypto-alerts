using System.Net;
using System.Net.Mail;

namespace CryptoAlerts.Worker.Infra.Email;

public sealed class GmailSmtpEmailSender
{
    private readonly EmailOptions _opt;

    public GmailSmtpEmailSender(EmailOptions opt)
        => _opt = opt;

    public async Task SendAsync(string subject, string body, CancellationToken ct)
    {
        using var client = new SmtpClient(_opt.SmtpHost, _opt.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_opt.FromEmail, _opt.AppPassword)
        };

        using var msg = new MailMessage(_opt.FromEmail, _opt.ToEmail, subject, body);

        await client.SendMailAsync(msg, ct);
    }
}
