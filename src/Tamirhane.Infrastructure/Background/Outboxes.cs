using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Background;

/// <summary>
/// E-postayı kuyruğa koyar; gönderim ve tekrar deneme arka planda. Kind loglarda görünür (alıcı görünmez).
/// ".invalid" ile biten adresler (RFC 2606, hiçbir zaman var olmaz; demo hesapları bunu kullanır) hiç kuyruğa girmez:
/// örneğin demo adresiyle "şifremi unuttum" denirse e-posta servisine geri dönen bir ileti gitmez.
/// </summary>
internal sealed class EmailOutbox(BackgroundJobQueue queue)
{
    public void Enqueue(EmailMessage message, string kind)
    {
        if (IsUndeliverable(message.To))
            return;
        queue.Enqueue(new BackgroundJob($"e-posta: {kind}",
            (services, ct) => services.GetRequiredService<IEmailSender>().SendAsync(message, ct)));
    }

    internal static bool IsUndeliverable(string address) =>
        address.TrimEnd().EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// İş emri durumu değişince müşteriye bildirim yapan bir kanal (e-posta #30, tarayıcı bildirimi #31).
/// Her kanal ayrı bir iş olarak çalışır: biri hata verip yeniden denenirken diğeri ikinci kez gönderilmez.
/// </summary>
public interface IWorkOrderNotificationHandler
{
    Task HandleAsync(WorkOrderNotification notification, CancellationToken ct);
}

/// <summary><see cref="INotificationQueue"/>: her bildirim kanalı için kuyruğa bir iş.</summary>
internal sealed class WorkOrderNotificationQueue(BackgroundJobQueue queue, IEnumerable<IWorkOrderNotificationHandler> handlers)
    : INotificationQueue
{
    public void Enqueue(WorkOrderNotification notification)
    {
        foreach (var type in handlers.Select(h => h.GetType()).Distinct())
            queue.Enqueue(new BackgroundJob($"bildirim: {type.Name} {notification.Status}", (services, ct) =>
                services.GetServices<IWorkOrderNotificationHandler>().First(h => h.GetType() == type).HandleAsync(notification, ct)));
    }
}
