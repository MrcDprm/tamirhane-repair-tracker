namespace Tamirhane.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string Text, string Html);

/// <summary>Tek bir e-postayı gönderir. Kuyruk ve tekrar deneme bunun üstünde (EmailOutbox, BackgroundJobWorker).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
