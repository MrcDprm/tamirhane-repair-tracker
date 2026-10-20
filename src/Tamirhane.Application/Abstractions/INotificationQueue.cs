using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Abstractions;

/// <summary>Müşteriye gidecek bildirim: iş emrinin yeni durumu (e-posta, tarayıcı bildirimi).</summary>
public sealed record WorkOrderNotification(Guid ShopId, Guid WorkOrderId, WorkOrderStatus Status);

/// <summary>
/// Bildirimler arka planda gönderilir; istek göndermeyi beklemez ve gönderim hatası iş emri işlemini bozmaz.
/// Kuyruğa kayıt başarıyla kaydedildikten sonra eklenmelidir.
/// </summary>
public interface INotificationQueue
{
    void Enqueue(WorkOrderNotification notification);
}
