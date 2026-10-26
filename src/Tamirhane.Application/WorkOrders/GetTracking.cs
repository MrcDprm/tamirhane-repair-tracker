using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>Takip sayfasındaki yolun bir durağı. Henüz gelinmemiş durakların zamanı yoktur.</summary>
public sealed record TrackingStep(WorkOrderStatus Status, DateTimeOffset? At);

public sealed record TrackingNote(string Text, DateTimeOffset At);

/// <summary>Teklif kalemi, müşterinin gördüğü kadarıyla (stok kodu ya da parça kimliği yok).</summary>
public sealed record TrackingQuoteLine(string Description, decimal Quantity, decimal UnitPrice, decimal DiscountRate, decimal VatRate, decimal Total);

/// <summary>Verilmiş karar; kararı verenin yazdığı ad sayfada gösterilmez.</summary>
public sealed record TrackingDecision(QuoteDecisionKind Kind, DateTimeOffset At);

/// <summary>
/// Müşterinin takip sayfası (giriş yok, erişim takip koduyla). Bilerek dar tutulur: müşterinin adı maskeli gelir,
/// tam adı, telefonu, e-postası, cihaz şifresi, IMEI/seri no, personel adları ve atölye içi notlar burada hiç yoktur.
/// </summary>
public sealed record TrackingView(
    string Number, string MaskedCustomerName, DeviceKind DeviceKind, string Brand, string Model, string Complaint,
    WorkOrderStatus Status, DateTimeOffset CreatedAt, DateOnly? PromisedDate, DateTimeOffset? DeliveredAt, DateTimeOffset? WarrantyUntil,
    string ShopName, string BranchName, string? BranchAddress, string? BranchPhone,
    IReadOnlyList<TrackingStep> Steps, IReadOnlyList<TrackingNote> Notes)
{
    public bool IsClosed => Status is WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled;

    /// <summary>Teklif sadece gönderildikten sonra görünür (inceleme sırasında hazırlanan kalemler taslaktır).</summary>
    public IReadOnlyList<TrackingQuoteLine> QuoteLines { get; init; } = [];
    public QuoteTotals QuoteTotals { get; init; } = QuoteTotals.Of([]);
    public TrackingDecision? Decision { get; init; }
    public bool CanDecide => Status == WorkOrderStatus.AwaitingApproval;
}

public sealed class GetTracking(IWorkOrderRepository orders, ITrackingDirectory directory)
{
    /// <summary>Bulunduğu durumdan sonra olağan akışta gelecek duraklar (teklif ve ret, olursa yolda görünür).</summary>
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> Ahead = new()
    {
        [WorkOrderStatus.Received] = [WorkOrderStatus.Diagnosing, WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
        [WorkOrderStatus.Diagnosing] = [WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
        [WorkOrderStatus.AwaitingApproval] = [WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
        [WorkOrderStatus.InRepair] = [WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
        [WorkOrderStatus.Ready] = [WorkOrderStatus.Delivered],
        [WorkOrderStatus.Declined] = [WorkOrderStatus.Delivered],
        [WorkOrderStatus.Delivered] = [],
        [WorkOrderStatus.Cancelled] = [],
    };

    /// <summary>Kod geçersizse de kayıt yoksa da aynı sonuç: hangisi olduğu dışarıya söylenmez.</summary>
    public async Task<TrackingView?> HandleAsync(string? trackingCode, CancellationToken ct)
    {
        var code = TrackingCode.Normalize(trackingCode);
        var order = code is null ? null : await orders.GetByTrackingCodeAsync(code, ct);
        if (order is null)
            return null;

        var parties = await directory.GetAsync(order.ShopId, order.BranchId, order.CustomerId, order.DeviceId, ct)
            ?? throw new InvalidOperationException("İş emrinin atölyesi, şubesi, müşterisi ya da cihazı yok.");

        var steps = order.History.OrderBy(h => h.At).Select(h => new TrackingStep(h.To, h.At))
            .Concat(Ahead[order.Status].Select(s => new TrackingStep(s, null)))
            .ToList();
        var notes = order.Notes.Where(n => n.VisibleToCustomer).OrderByDescending(n => n.At)
            .Select(n => new TrackingNote(n.Text, n.At)).ToList();

        // Gönderilmemiş teklif (inceleme sırasındaki taslak) ve teklifsiz onarım müşteriye gösterilmez.
        var quoteSent = order.Status == WorkOrderStatus.AwaitingApproval || order.QuoteDecision is not null;
        return new TrackingView(
            order.Number, NameMask.Mask(parties.CustomerName), parties.DeviceKind, parties.Brand, parties.Model, order.Complaint,
            order.Status, order.CreatedAt, order.PromisedDate, order.DeliveredAt, order.WarrantyUntil,
            parties.ShopName, parties.BranchName, parties.BranchAddress, parties.BranchPhone,
            steps, notes)
        {
            QuoteLines = quoteSent
                ? order.QuoteLines.Select(l => new TrackingQuoteLine(l.Description, l.Quantity, l.UnitPrice, l.DiscountRate, l.VatRate, l.Total)).ToList()
                : [],
            QuoteTotals = quoteSent ? order.QuoteTotals : QuoteTotals.Of([]),
            Decision = order.QuoteDecision is { } d ? new TrackingDecision(d.Kind, d.At) : null,
        };
    }
}
