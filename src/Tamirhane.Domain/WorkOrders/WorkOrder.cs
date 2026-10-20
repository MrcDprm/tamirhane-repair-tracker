using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// Tamire bırakılan bir cihazın iş emri. Durum sadece bu sınıfın metotlarıyla değişir;
/// hangi durumdan hangisine geçilebileceği <see cref="AllowedTransitions"/> tablosundadır.
/// </summary>
public sealed class WorkOrder : Entity
{
    public const int MaxTextLength = 1000;

    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> AllowedTransitions = new()
    {
        [WorkOrderStatus.Received] = [WorkOrderStatus.Diagnosing, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Diagnosing] = [WorkOrderStatus.AwaitingApproval, WorkOrderStatus.InRepair, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.AwaitingApproval] = [WorkOrderStatus.InRepair, WorkOrderStatus.Declined, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.InRepair] = [WorkOrderStatus.Ready, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Ready] = [WorkOrderStatus.Delivered, WorkOrderStatus.Cancelled],
        // Teklif reddedildi: cihaz onarılmadan müşteriye geri verilir.
        [WorkOrderStatus.Declined] = [WorkOrderStatus.Delivered, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Delivered] = [],
        [WorkOrderStatus.Cancelled] = [],
    };

    private const int MaxDeciderNameLength = 100;

    private readonly List<StatusChange> _history = [];
    private readonly List<QuoteLine> _quoteLines = [];

    private WorkOrder() { }

    public Guid ShopId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Number { get; private set; } = "";
    public string TrackingCode { get; private set; } = "";
    public Guid CustomerId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string Complaint { get; private set; } = "";
    public string? Condition { get; private set; }
    public string? Accessories { get; private set; }
    public string? Passcode { get; private set; }
    public WorkOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public IReadOnlyList<StatusChange> History => _history;
    public IReadOnlyList<QuoteLine> QuoteLines => _quoteLines;
    public QuoteDecision? QuoteDecision { get; private set; }
    public QuoteTotals QuoteTotals => QuoteTotals.Of(_quoteLines);

    public bool IsClosed => Status is WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled;

    /// <summary>
    /// Teklif, inceleme sırasında hazırlanır. Teklifsiz başlayan onarımda (ücretsiz ya da garanti işi)
    /// kullanılan parçalar onarım sırasında da eklenebilir. Müşteri karar verdikten sonra kalemler kilitlenir.
    /// </summary>
    public bool IsQuoteEditable =>
        Status == WorkOrderStatus.Diagnosing
        || (Status == WorkOrderStatus.InRepair && QuoteDecision is null);

    public static Result<WorkOrder> Receive(
        Guid shopId, Guid branchId, string number, string trackingCode, Guid customerId, Guid deviceId,
        string complaint, string? condition, string? accessories, string? passcode,
        Guid staffId, DateTimeOffset now)
    {
        complaint = complaint?.Trim() ?? "";
        if (complaint.Length == 0)
            return WorkOrderErrors.ComplaintRequired;
        if (TooLong(complaint) || TooLong(condition) || TooLong(accessories) || TooLong(passcode))
            return WorkOrderErrors.TextTooLong;

        var order = new WorkOrder
        {
            ShopId = shopId,
            BranchId = branchId,
            Number = number,
            TrackingCode = trackingCode,
            CustomerId = customerId,
            DeviceId = deviceId,
            Complaint = complaint,
            Condition = Clean(condition),
            Accessories = Clean(accessories),
            Passcode = Clean(passcode),
            Status = WorkOrderStatus.Received,
            CreatedAt = now,
        };
        order._history.Add(new StatusChange(order.Id, null, WorkOrderStatus.Received, staffId, now, null));
        return order;
    }

    public bool CanMoveTo(WorkOrderStatus to) => AllowedTransitions[Status].Contains(to);

    public Result StartDiagnosis(Guid staffId, DateTimeOffset now) =>
        MoveTo(WorkOrderStatus.Diagnosing, staffId, now, null);

    public Result<QuoteLine> AddQuoteLine(
        QuoteLineKind kind, Guid? partId, string description,
        decimal quantity, decimal unitPrice, decimal vatRate, decimal discountRate)
    {
        if (!IsQuoteEditable)
            return QuoteErrors.NotEditable;

        var line = QuoteLine.Create(Id, kind, partId, description, quantity, unitPrice, vatRate, discountRate);
        if (line.IsSuccess)
            _quoteLines.Add(line.Value);
        return line;
    }

    public Result RemoveQuoteLine(Guid lineId)
    {
        if (!IsQuoteEditable)
            return QuoteErrors.NotEditable;

        var line = _quoteLines.Find(l => l.Id == lineId);
        if (line is null)
            return QuoteErrors.LineNotFound;

        _quoteLines.Remove(line);
        return Result.Success();
    }

    public Result SendQuote(Guid staffId, DateTimeOffset now)
    {
        if (Status != WorkOrderStatus.Diagnosing)
            return WorkOrderErrors.InvalidTransition;
        if (_quoteLines.Count == 0)
            return QuoteErrors.Empty;

        return MoveTo(WorkOrderStatus.AwaitingApproval, staffId, now, null);
    }

    /// <summary>
    /// Müşterinin kararı. Takip sayfasından gelirse staffId boştur; müşteri telefonla bildirdiyse
    /// personel onun adına kaydeder. Onay onarımı başlatır, red cihazı onarılmadan teslime hazırlar.
    /// </summary>
    public Result DecideQuote(bool approved, string byName, Guid? staffId, DateTimeOffset now)
    {
        byName = byName?.Trim() ?? "";
        if (byName.Length == 0 || byName.Length > MaxDeciderNameLength)
            return QuoteErrors.DeciderNameRequired;
        if (Status != WorkOrderStatus.AwaitingApproval)
            return WorkOrderErrors.InvalidTransition;

        var kind = approved ? QuoteDecisionKind.Approved : QuoteDecisionKind.Declined;
        var result = MoveTo(approved ? WorkOrderStatus.InRepair : WorkOrderStatus.Declined, staffId, now, null);
        if (result.IsSuccess)
            QuoteDecision = new QuoteDecision(kind, byName, now, staffId);
        return result;
    }

    /// <summary>Teklif gerekmeyen onarım (ör. ücretsiz ya da garanti kapsamında): incelemeden doğrudan onarıma.</summary>
    public Result StartRepair(Guid staffId, DateTimeOffset now) =>
        Status == WorkOrderStatus.Diagnosing
            ? MoveTo(WorkOrderStatus.InRepair, staffId, now, null)
            : WorkOrderErrors.InvalidTransition;

    public Result MarkReady(Guid staffId, DateTimeOffset now) =>
        MoveTo(WorkOrderStatus.Ready, staffId, now, null);

    public Result Deliver(Guid staffId, DateTimeOffset now)
    {
        var result = MoveTo(WorkOrderStatus.Delivered, staffId, now, null);
        if (result.IsSuccess)
        {
            DeliveredAt = now;
            // Cihaz şifresi sadece onarım süresince gerekir; teslimde saklanmaya devam etmez.
            Passcode = null;
        }
        return result;
    }

    public Result Cancel(Guid staffId, DateTimeOffset now, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0)
            return WorkOrderErrors.CancelReasonRequired;
        if (TooLong(reason))
            return WorkOrderErrors.TextTooLong;

        var result = MoveTo(WorkOrderStatus.Cancelled, staffId, now, reason);
        if (result.IsSuccess)
            Passcode = null;
        return result;
    }

    private Result MoveTo(WorkOrderStatus to, Guid? staffId, DateTimeOffset now, string? note)
    {
        if (!CanMoveTo(to))
            return WorkOrderErrors.InvalidTransition;

        _history.Add(new StatusChange(Id, Status, to, staffId, now, note));
        Status = to;
        return Result.Success();
    }

    private static bool TooLong(string? text) => text is not null && text.Length > MaxTextLength;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
