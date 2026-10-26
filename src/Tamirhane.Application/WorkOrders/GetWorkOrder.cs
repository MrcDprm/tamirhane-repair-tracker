using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>İş emri sayfasındaki işlemler. Sayfa sadece o an geçerli olanları gösterir.</summary>
public enum WorkOrderAction
{
    StartDiagnosis,
    SendQuote,
    StartRepair,
    RecordApproval,
    RecordDecline,
    MarkReady,
    Deliver,
    Cancel,
}

/// <summary>Durum geçmişinde bir adım: hangi duruma, ne zaman, kim (müşterinin kendi kararında boş).</summary>
public sealed record StatusStep(WorkOrderStatus Status, DateTimeOffset At, string? By, string? Note);

public sealed record NoteView(string Text, bool VisibleToCustomer, string By, DateTimeOffset At);

/// <summary>
/// İş emri sayfasının bilgileri (atölye içi; müşterinin tam adı ve telefonu görünür).
/// Cihaz şifresi sayfaya gönderilmez, sadece kayıtlı olup olmadığı.
/// </summary>
public sealed record WorkOrderView(
    Guid Id, string Number, string TrackingCode, WorkOrderStatus Status, DateTimeOffset CreatedAt,
    string Complaint, string? Condition, string? Accessories, bool HasPasscode,
    string CustomerName, string CustomerPhone, string? CustomerEmail,
    DeviceKind DeviceKind, string Brand, string Model, string? Serial, string? Imei)
{
    public IReadOnlyList<StatusStep> History { get; init; } = [];
    public IReadOnlyList<NoteView> Notes { get; init; } = [];
    /// <summary>Geçerli işlemler; ilki sıradaki adım (sayfadaki tek birincil düğme).</summary>
    public IReadOnlyList<WorkOrderAction> Actions { get; init; } = [];
    public int QuoteLineCount { get; init; }
    public decimal AmountDue { get; init; }
    public decimal Balance { get; init; }
    public DateTimeOffset? WarrantyUntil { get; init; }

    public WorkOrderAction? NextAction => Actions.Count > 0 && Actions[0] != WorkOrderAction.Cancel ? Actions[0] : null;
}

public sealed class GetWorkOrder(
    IWorkOrderRepository orders, ICustomerRepository customers, IDeviceRepository devices, IStaffRepository staff)
{
    public async Task<Result<WorkOrderView>> HandleAsync(Guid id, CancellationToken ct)
    {
        // Başka atölyenin iş emri kiracı filtresinden dolayı bulunmaz: "yok" döner, varlığı sızmaz.
        var order = await orders.GetAsync(id, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var customer = await customers.GetAsync(order.CustomerId, ct) ?? throw new InvalidOperationException("İş emrinin müşterisi yok.");
        var device = await devices.GetAsync(order.DeviceId, ct) ?? throw new InvalidOperationException("İş emrinin cihazı yok.");
        // Çıkarılmış personelin adı da gelir: geçmişte kimin ne yaptığı kaybolmaz.
        var names = await staff.NamesAsync(
            order.History.Select(h => h.StaffId).OfType<Guid>().Concat(order.Notes.Select(n => n.StaffId)), ct);

        return new WorkOrderView(
            order.Id, order.Number, order.TrackingCode, order.Status, order.CreatedAt,
            order.Complaint, order.Condition, order.Accessories, order.Passcode is not null,
            customer.Name, customer.Phone.Value, customer.Email,
            device.Kind, device.Brand, device.Model, device.Serial, device.Imei)
        {
            History = order.History.OrderBy(h => h.At)
                .Select(h => new StatusStep(h.To, h.At, h.StaffId is { } s ? names.GetValueOrDefault(s) : null, h.Note)).ToList(),
            Notes = order.Notes.OrderByDescending(n => n.At)
                .Select(n => new NoteView(n.Text, n.VisibleToCustomer, names.GetValueOrDefault(n.StaffId, "?"), n.At)).ToList(),
            Actions = ActionsFor(order),
            QuoteLineCount = order.QuoteLines.Count,
            AmountDue = order.AmountDue,
            Balance = order.Balance,
            WarrantyUntil = order.WarrantyUntil,
        };
    }

    /// <summary>
    /// Durum makinesine göre sayfada görünecek işlemler, sıradaki adım başta. Teklif kalemi varsa incelemeden sonra
    /// sıradaki adım teklifi göndermek; yoksa teklifsiz onarım (ücretsiz ya da garanti işi). İptal her açık durumda sonda.
    /// </summary>
    public static IReadOnlyList<WorkOrderAction> ActionsFor(WorkOrder order)
    {
        List<WorkOrderAction> actions = order.Status switch
        {
            WorkOrderStatus.Received => [WorkOrderAction.StartDiagnosis],
            WorkOrderStatus.Diagnosing when order.QuoteLines.Count > 0 => [WorkOrderAction.SendQuote, WorkOrderAction.StartRepair],
            WorkOrderStatus.Diagnosing => [WorkOrderAction.StartRepair],
            WorkOrderStatus.AwaitingApproval => [WorkOrderAction.RecordApproval, WorkOrderAction.RecordDecline],
            WorkOrderStatus.InRepair => [WorkOrderAction.MarkReady],
            WorkOrderStatus.Ready or WorkOrderStatus.Declined => [WorkOrderAction.Deliver],
            _ => [],
        };
        if (order.CanMoveTo(WorkOrderStatus.Cancelled))
            actions.Add(WorkOrderAction.Cancel);
        return actions;
    }
}
