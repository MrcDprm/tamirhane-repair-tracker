using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Persistence;
using DomainSubscription = Tamirhane.Domain.WorkOrders.PushSubscription;
using LibSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace Tamirhane.Infrastructure.Notifications;

/// <summary>
/// Tarayıcı bildirimi (#31): iş emrinin durumu değişince abone olan her tarayıcıya bir bildirim. Bu iş sadece
/// bildirimi hazırlar ve her abonelik için ayrı bir gönderim işi koyar: biri hata verip yeniden denenirken
/// diğer tarayıcılara ikinci kez gitmez.
/// </summary>
internal sealed class CustomerPushNotifications(TamirhaneDbContext db, BackgroundJobQueue queue, ITrackingLinks links)
    : IWorkOrderNotificationHandler
{
    public async Task HandleAsync(WorkOrderNotification notification, CancellationToken ct)
    {
        var order = await db.WorkOrders.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == notification.WorkOrderId && o.ShopId == notification.ShopId, ct);
        // Kuyrukta beklerken durum yine değiştiyse eski haberi gönderme: yeni durumun bildirimi zaten sırada.
        if (order is null || order.Status != notification.Status)
            return;

        var subscriptions = await db.Set<DomainSubscription>().AsNoTracking()
            .Where(s => s.WorkOrderId == order.Id).Select(s => s.Id).ToListAsync(ct);
        if (subscriptions.Count == 0)
            return;

        var locale = await db.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.Id == order.ShopId).Select(s => s.Locale).SingleAsync(ct);
        var device = await db.Devices.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == order.DeviceId && d.ShopId == order.ShopId)
            .Select(d => d.Brand + " " + d.Model).SingleAsync(ct);

        var payload = Payload(order.Status, locale, device, links.For(order.TrackingCode));
        // Topic: push servisi aynı konudaki teslim edilmemiş eski bildirimi yenisiyle değiştirir; telefon kapalıyken
        // "inceleniyor" ve "onarılıyor" üst üste birikmez. 32 karakter, URL'de güvenli.
        var topic = order.Id.ToString("N");
        // İş emri kapandı: bu son haber. Gönderildikten sonra abonelik silinir; artık haber verilecek bir şey yok.
        var last = order.IsClosed;
        foreach (var id in subscriptions)
            queue.Enqueue(new BackgroundJob($"bildirim: tarayıcı {order.Status}", (services, token) =>
                services.GetRequiredService<PushDelivery>().SendAsync(id, payload, topic, last, token)));
    }

    /// <summary>Service worker'ın gösterdiği bildirim: başlık, metin ve tıklanınca açılacak takip sayfası.</summary>
    internal static string Payload(WorkOrderStatus status, string locale, string device, Uri trackingLink)
    {
        var (headline, lead) = StatusTexts.For(status, locale);
        return JsonSerializer.Serialize(new { title = $"{device}: {headline}", body = lead, url = trackingLink.AbsoluteUri });
    }
}

/// <summary>
/// Tek bir tarayıcıya gönderim. Push servisi aboneliğin artık geçersiz olduğunu söylerse (404, 410: kullanıcı izni
/// kaldırdı ya da tarayıcı aboneliği yeniledi) abonelik silinir ve iş başarılı sayılır. Diğer hatalar fırlatılır;
/// arka plan kuyruğu bu işi yeniden dener.
/// </summary>
internal sealed class PushDelivery(TamirhaneDbContext db, PushServiceClient client)
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromHours(24);

    public async Task SendAsync(Guid subscriptionId, string payload, string topic, bool deleteAfter, CancellationToken ct)
    {
        var subscription = await db.Set<DomainSubscription>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (subscription is null)
            return; // bu arada abonelikten çıkıldı

        var target = new LibSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);
        var message = new PushMessage(payload) { Topic = topic, TimeToLive = (int)TimeToLive.TotalSeconds, Urgency = PushMessageUrgency.Normal };

        try
        {
            await client.RequestPushMessageDeliveryAsync(target, message, ct);
        }
        catch (PushServiceClientException e) when (e.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            deleteAfter = true;
        }

        if (deleteAfter)
            await db.Set<DomainSubscription>().Where(s => s.Id == subscriptionId).ExecuteDeleteAsync(ct);
    }
}
