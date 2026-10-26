namespace Tamirhane.Infrastructure.Email;

/// <summary>Bir e-posta. FromName boşsa EMAIL_FROM'daki ad kullanılır (müşteri bildirimlerinde atölyenin adı).</summary>
public sealed record EmailMessage(string To, string Subject, string Text, string Html, string? FromName = null);

/// <summary>Tek bir e-postayı gönderir. Kuyruk ve tekrar deneme bunun üstünde (EmailOutbox, BackgroundJobWorker).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
