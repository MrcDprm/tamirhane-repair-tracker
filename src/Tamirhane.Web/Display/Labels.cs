using Microsoft.Extensions.Localization;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Web.Display;

/// <summary>
/// Ekranda görünen adlar: enum değeri → Resources/*.json anahtarı (#35). Görünümde <c>T.Of(Model.Status)</c> gibi kullanılır;
/// metin o anki arayüz dilinde gelir.
/// </summary>
public static class Labels
{
    public static readonly IReadOnlyList<DeviceKind> DeviceKinds =
    [
        DeviceKind.Phone, DeviceKind.Tablet, DeviceKind.Computer, DeviceKind.Console,
        DeviceKind.Watch, DeviceKind.Appliance, DeviceKind.Other,
    ];

    public static string Of(this IStringLocalizer t, DeviceKind kind) => t[kind switch
    {
        DeviceKind.Phone => "kind.phone",
        DeviceKind.Tablet => "kind.tablet",
        DeviceKind.Computer => "kind.computer",
        DeviceKind.Console => "kind.console",
        DeviceKind.Watch => "kind.watch",
        DeviceKind.Appliance => "kind.appliance",
        _ => "kind.other",
    }];

    public static string Of(this IStringLocalizer t, WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Received => t["status.received"],
        WorkOrderStatus.Diagnosing => t["status.diagnosing"],
        WorkOrderStatus.AwaitingApproval => t["status.awaiting_approval"],
        WorkOrderStatus.InRepair => t["status.in_repair"],
        WorkOrderStatus.Ready => t["status.ready"],
        WorkOrderStatus.Delivered => t["status.delivered"],
        WorkOrderStatus.Declined => t["status.declined"],
        WorkOrderStatus.Cancelled => t["status.cancelled"],
        _ => status.ToString(),
    };

    /// <summary>İş emri sayfasındaki düğmelerin metni: ne olacağını söyler.</summary>
    public static string Of(this IStringLocalizer t, WorkOrderAction action) => action switch
    {
        WorkOrderAction.StartDiagnosis => t["action.start_diagnosis"],
        WorkOrderAction.SendQuote => t["action.send_quote"],
        WorkOrderAction.StartRepair => t["action.start_repair"],
        WorkOrderAction.RecordApproval => t["action.record_approval"],
        WorkOrderAction.RecordDecline => t["action.record_decline"],
        WorkOrderAction.MarkReady => t["action.mark_ready"],
        WorkOrderAction.Deliver => t["action.deliver"],
        WorkOrderAction.Cancel => t["action.cancel"],
        _ => action.ToString(),
    };

    public static string Of(this IStringLocalizer t, WorkOrderFilter filter) => filter switch
    {
        WorkOrderFilter.Open => t["filter.open"],
        WorkOrderFilter.AwaitingApproval => t["status.awaiting_approval"],
        WorkOrderFilter.InRepair => t["status.in_repair"],
        WorkOrderFilter.Ready => t["status.ready"],
        WorkOrderFilter.Overdue => t["filter.overdue", ListWorkOrders.OverdueDays],
        WorkOrderFilter.Closed => t["filter.closed"],
        WorkOrderFilter.All => t["common.all"],
        _ => filter.ToString(),
    };

    /// <summary>"+905321234567" → "0532 123 45 67"; yurt dışı numaralar olduğu gibi.</summary>
    public static string Phone(string e164) =>
        e164.Length == 13 && e164.StartsWith("+90")
            ? $"0{e164[3..6]} {e164[6..9]} {e164[9..11]} {e164[11..13]}"
            : e164;
}
