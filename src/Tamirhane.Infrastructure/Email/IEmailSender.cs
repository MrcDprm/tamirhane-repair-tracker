namespace Tamirhane.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string Text, string Html);

/// <summary>Tek bir e-postayı gönderir. Kuyruk ve tekrar deneme bunun üstünde (#19).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
