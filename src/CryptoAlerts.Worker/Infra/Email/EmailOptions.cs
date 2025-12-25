namespace CryptoAlerts.Worker.Infra.Email;

public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;

    public string FromEmail { get; set; } = "";
    public string ToEmail { get; set; } = "";

    // Gmail App Password (não é a senha normal)
    public string AppPassword { get; set; } = "";
}
