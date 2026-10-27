using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure.Notifications;

/// <summary>
/// Müşteriye durum e-postası (#30): iş emrinin durumu değişince, müşterinin e-postası varsa takip linkiyle bir e-posta.
/// Bu iş sadece e-postayı hazırlayıp kuyruğa koyar; gönderim ve tekrar deneme ayrı bir iştir. Gönderim başarısız
/// olsa da iş emri işlemi etkilenmez: işlem kaydedildikten sonra kuyruğa girmiştir.
/// Arka planda oturum olmadan çalışır: kiracı filtresi kapalıdır, her sorgu atölye kimliğiyle süzülür.
/// </summary>
internal sealed class CustomerStatusEmails(TamirhaneDbContext db, EmailOutbox outbox, ITrackingLinks links)
    : IWorkOrderNotificationHandler
{
    public async Task HandleAsync(WorkOrderNotification notification, CancellationToken ct)
    {
        var order = await db.WorkOrders.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == notification.WorkOrderId && o.ShopId == notification.ShopId, ct);
        // İş kuyrukta beklerken durum yine değiştiyse eski haberi gönderme: yeni durumun e-postası zaten sırada.
        if (order is null || order.Status != notification.Status)
            return;

        var email = await db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == order.CustomerId && c.ShopId == order.ShopId)
            .Select(c => c.Email).SingleOrDefaultAsync(ct);
        if (email is null)
            return;

        var shop = await db.Shops.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == order.ShopId, ct);
        var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == order.BranchId && b.ShopId == order.ShopId)
            .Select(b => new { b.Address, b.Phone }).SingleOrDefaultAsync(ct);
        var device = await db.Devices.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == order.DeviceId && d.ShopId == order.ShopId)
            .Select(d => d.Brand + " " + d.Model).SingleAsync(ct);

        var message = Build(email, shop.Locale, shop.Name, order.Number, device, order.Status,
            branch?.Address, branch?.Phone, links.For(order.TrackingCode));
        outbox.Enqueue(message, $"müşteri durumu {order.Status}");
    }

    /// <summary>Durum e-postası. Metinler takip sayfasıyla aynı dilde; gönderen adı atölyenin adı.</summary>
    internal static EmailMessage Build(
        string to, string locale, string shopName, string number, string device, WorkOrderStatus status,
        string? address, string? phone, Uri trackingLink)
    {
        var en = locale == "en";
        var (headline, lead) = en ? English(status) : Turkish(status);
        var paragraphs = new List<string> { lead };
        if ((status is WorkOrderStatus.Ready or WorkOrderStatus.Declined) && (address ?? phone) is not null)
            paragraphs.Add(en
                ? $"Pick-up address: {Join(address, phone)}"
                : $"Teslim adresi: {Join(address, phone)}");
        paragraphs.Add(en ? $"Work order: {number} ({device})" : $"İş emri: {number} ({device})");

        return EmailLayout.Build(to, locale, $"{device}: {headline}", paragraphs,
            new EmailAction(en ? "Track your repair" : "Onarımı takip et", trackingLink),
            en
                ? $"You get this email because your email address is on your service record at {shopName}. The link is personal; please do not share it."
                : $"Bu e-postayı {shopName} servis kaydında e-posta adresin olduğu için alıyorsun. Link sana özel, paylaşma.",
            fromName: shopName);
    }

    private static string Join(string? address, string? phone) =>
        string.Join(" · ", new[] { address, phone is null ? null : DisplayPhone(phone) }.Where(p => p is not null));

    /// <summary>"+905321234567" → "0532 123 45 67"; yurt dışı numaralar olduğu gibi.</summary>
    private static string DisplayPhone(string e164) =>
        e164.Length == 13 && e164.StartsWith("+90")
            ? $"0{e164[3..6]} {e164[6..9]} {e164[9..11]} {e164[11..13]}"
            : e164;

    private static (string Headline, string Lead) Turkish(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => ("teslim alındı", "Cihazın atölyede. Sırası gelince incelenecek; durumunu aşağıdaki linkten takip edebilirsin."),
        WorkOrderStatus.Diagnosing => ("inceleniyor", "Cihazın inceleniyor. Arıza bulununca fiyatı bildireceğiz."),
        WorkOrderStatus.AwaitingApproval => ("teklif hazır", "Onarım teklifi hazır. Takip sayfasından teklifi görüp onaylayabilir ya da reddedebilirsin."),
        WorkOrderStatus.InRepair => ("onarılıyor", "Cihazın onarılıyor. Bitince haber vereceğiz."),
        WorkOrderStatus.Ready => ("teslime hazır", "Cihazın hazır. Fişinle gelip teslim alabilirsin."),
        WorkOrderStatus.Declined => ("onarım yapılmayacak", "Onarım yapılmayacak. Cihazını gelip teslim alabilirsin."),
        WorkOrderStatus.Delivered => ("teslim edildi", "Cihazın teslim edildi. Garanti bilgisi takip sayfasında."),
        WorkOrderStatus.Cancelled => ("iş emri iptal edildi", "Bu iş emri iptal edildi. Bir sorun olduğunu düşünüyorsan atölyeyi ara."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static (string Headline, string Lead) English(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => ("received", "Your device is at the shop and will be checked in turn; you can follow it with the link below."),
        WorkOrderStatus.Diagnosing => ("being checked", "Your device is being checked. We will let you know the price once we find the fault."),
        WorkOrderStatus.AwaitingApproval => ("quote ready", "The repair quote is ready. You can see it and approve or decline it on the tracking page."),
        WorkOrderStatus.InRepair => ("being repaired", "Your device is being repaired. We will let you know when it is done."),
        WorkOrderStatus.Ready => ("ready for pick-up", "Your device is ready. Bring your receipt and pick it up."),
        WorkOrderStatus.Declined => ("no repair", "The repair will not be done. You can come and pick up your device."),
        WorkOrderStatus.Delivered => ("picked up", "Your device has been handed over. Warranty details are on the tracking page."),
        WorkOrderStatus.Cancelled => ("work order cancelled", "This work order was cancelled. If you think something is wrong, call the shop."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
