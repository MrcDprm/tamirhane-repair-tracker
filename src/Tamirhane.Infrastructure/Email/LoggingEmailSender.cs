using Microsoft.Extensions.Logging;

namespace Tamirhane.Infrastructure.Email;

/// <summary>
/// Geliştirmede (RESEND_API_KEY yokken) e-posta göndermek yerine metnini loga yazar; doğrulama linkine
/// konsoldan tıklanır. Alıcı adresi loga yazılmaz.
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation("E-posta gönderilmedi (geliştirme): {Subject}{NewLine}{Text}",
            message.Subject, Environment.NewLine, message.Text);
        return Task.CompletedTask;
    }
}
