using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Web.Display;

/// <summary>Ekranda görünen adlar (Türkçe; İngilizcesi #35'te).</summary>
public static class Labels
{
    public static readonly IReadOnlyList<(DeviceKind Kind, string Label)> DeviceKinds =
    [
        (DeviceKind.Phone, "Telefon"),
        (DeviceKind.Tablet, "Tablet"),
        (DeviceKind.Computer, "Bilgisayar"),
        (DeviceKind.Console, "Oyun konsolu"),
        (DeviceKind.Watch, "Akıllı saat"),
        (DeviceKind.Appliance, "Ev aleti"),
        (DeviceKind.Other, "Diğer"),
    ];

    public static string Of(DeviceKind kind) => DeviceKinds.First(k => k.Kind == kind).Label;

    public static string Of(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => "Teslim alındı",
        WorkOrderStatus.Diagnosing => "Arıza tespiti",
        WorkOrderStatus.AwaitingApproval => "Onay bekliyor",
        WorkOrderStatus.InRepair => "Onarımda",
        WorkOrderStatus.Ready => "Teslime hazır",
        WorkOrderStatus.Delivered => "Teslim edildi",
        WorkOrderStatus.Declined => "Onarım istenmedi",
        WorkOrderStatus.Cancelled => "İptal",
        _ => status.ToString(),
    };

    /// <summary>İş emri sayfasındaki düğmelerin metni: ne olacağını söyler.</summary>
    public static string Of(WorkOrderAction action) => action switch
    {
        WorkOrderAction.StartDiagnosis => "İncelemeye başla",
        WorkOrderAction.SendQuote => "Teklifi müşteriye gönder",
        WorkOrderAction.StartRepair => "Teklifsiz onarıma başla",
        WorkOrderAction.RecordApproval => "Müşteri onayladı",
        WorkOrderAction.RecordDecline => "Müşteri reddetti",
        WorkOrderAction.MarkReady => "Onarım bitti, teslime hazır",
        WorkOrderAction.Deliver => "Müşteriye teslim et",
        WorkOrderAction.Cancel => "İş emrini iptal et",
        _ => action.ToString(),
    };

    /// <summary>"+905321234567" → "0532 123 45 67"; yurt dışı numaralar olduğu gibi.</summary>
    public static string Phone(string e164) =>
        e164.Length == 13 && e164.StartsWith("+90")
            ? $"0{e164[3..6]} {e164[6..9]} {e164[9..11]} {e164[11..13]}"
            : e164;
}
