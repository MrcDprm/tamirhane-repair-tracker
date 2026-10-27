using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Common;

/// <summary>
/// Müşteriye giden durum haberlerinin metni: e-posta (#30), tarayıcı bildirimi (#31) ve WhatsApp mesajı (#32) aynısını kullanır.
/// Başlık kısa ("teslime hazır"), açıklama takip sayfasındaki cümleyle aynı dilde.
/// </summary>
public static class StatusTexts
{
    public static (string Headline, string Lead) For(WorkOrderStatus status, string locale) =>
        locale == "en" ? English(status) : Turkish(status);

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
