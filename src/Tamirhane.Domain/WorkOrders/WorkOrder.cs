using Tamirhane.Domain.Common;
using Tamirhane.Domain.Inventory;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// Tamire bırakılan bir cihazın iş emri. Durum sadece bu sınıfın metotlarıyla değişir;
/// hangi durumdan hangisine geçilebileceği <see cref="AllowedTransitions"/> tablosundadır.
/// </summary>
public sealed class WorkOrder : Entity
{
    public const int MaxTextLength = 1000;
    public const int MaxWarrantyDays = 3650;
    /// <summary>Söz verilen teslim günü en fazla bu kadar ileri olabilir (yanlış yıl yazımına karşı).</summary>
    public const int MaxPromiseDays = 180;

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
    private readonly List<Payment> _payments = [];
    private readonly List<WorkOrderNote> _notes = [];

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
    /// <summary>Müşteriye söz verilen teslim günü; takip sayfasında "tahmini teslim". Boş olabilir.</summary>
    public DateOnly? PromisedDate { get; private set; }
    /// <summary>Teklifteki stok parçaları şubenin stoğundan düşüldü mü (iş emri hazır olduğunda).</summary>
    public bool PartsConsumed { get; private set; }
    public IReadOnlyList<StatusChange> History => _history;
    public IReadOnlyList<QuoteLine> QuoteLines => _quoteLines;
    public QuoteDecision? QuoteDecision { get; private set; }
    public QuoteTotals QuoteTotals => QuoteTotals.Of(_quoteLines);
    public IReadOnlyList<Payment> Payments => _payments;
    public IReadOnlyList<WorkOrderNote> Notes => _notes;
    /// <summary>Teklif reddedildiğinde istenen inceleme ücreti (KDV dahil). Varsayılan 0.</summary>
    public decimal DiagnosisFee { get; private set; }

    /// <summary>Müşterinin ödemesi gereken tutar: teklif reddedildiyse inceleme ücreti, değilse teklifin toplamı.</summary>
    public decimal AmountDue => QuoteDecision?.Kind == QuoteDecisionKind.Declined ? DiagnosisFee : QuoteTotals.Total;
    public decimal PaidTotal => _payments.Sum(p => p.SignedAmount);
    public decimal Balance => AmountDue - PaidTotal;
    /// <summary>Borçlu teslim edildiyse teslim anındaki borç; borçsuz teslimde boş.</summary>
    public decimal? BalanceAtDelivery { get; private set; }
    /// <summary>Garantinin bittiği an; onarılmadan iade edilen ya da garantisiz teslim edilen cihazda boş.</summary>
    public DateTimeOffset? WarrantyUntil { get; private set; }
    /// <summary>Garanti kapsamında geri gelen cihazda önceki iş emri.</summary>
    public Guid? WarrantyOfId { get; private set; }
    public bool IsWarrantyReturn => WarrantyOfId is not null;

    public bool IsUnderWarranty(DateTimeOffset now) => WarrantyUntil is not null && now <= WarrantyUntil;

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

    /// <summary>
    /// Garanti kapsamında geri gelen cihaz: yeni iş emri öncekine bağlanır, müşteri ve cihaz öncekinden alınır.
    /// Önceki iş emri teslim edilmiş ve garantisi sürüyor olmalıdır.
    /// </summary>
    public static Result<WorkOrder> ReceiveUnderWarranty(
        WorkOrder previous, Guid branchId, string number, string trackingCode,
        string complaint, string? condition, string? accessories, string? passcode,
        Guid staffId, DateTimeOffset now)
    {
        if (previous.Status != WorkOrderStatus.Delivered || !previous.IsUnderWarranty(now))
            return WorkOrderErrors.NotUnderWarranty;

        var order = Receive(previous.ShopId, branchId, number, trackingCode, previous.CustomerId, previous.DeviceId,
            complaint, condition, accessories, passcode, staffId, now);
        if (order.IsSuccess)
            order.Value.WarrantyOfId = previous.Id;
        return order;
    }

    /// <summary>
    /// Teslim gününü söz verir ya da değiştirir (parça gecikti); boş verilirse söz kaldırılır.
    /// Geçmiş bir gün söz verilemez; iş emri kapanınca değişmez.
    /// </summary>
    public Result Promise(DateOnly? date, DateOnly today)
    {
        if (IsClosed)
            return WorkOrderErrors.Closed;
        if (date is { } d && (d < today || d > today.AddDays(MaxPromiseDays)))
            return WorkOrderErrors.InvalidPromisedDate;

        PromisedDate = date;
        return Result.Success();
    }

    /// <summary>Kabulde yanlış girilen bilgileri düzeltir; iş emri kapanınca değişmez.</summary>
    public Result UpdateIntake(string complaint, string? condition, string? accessories, string? passcode)
    {
        if (IsClosed)
            return WorkOrderErrors.Closed;
        complaint = complaint?.Trim() ?? "";
        if (complaint.Length == 0)
            return WorkOrderErrors.ComplaintRequired;
        if (TooLong(complaint) || TooLong(condition) || TooLong(accessories) || TooLong(passcode))
            return WorkOrderErrors.TextTooLong;

        Complaint = complaint;
        Condition = Clean(condition);
        Accessories = Clean(accessories);
        Passcode = Clean(passcode);
        return Result.Success();
    }

    /// <summary>Not her zaman eklenebilir; teslimden sonra da (ör. "Müşteri memnun kaldığını söyledi").</summary>
    public Result<WorkOrderNote> AddNote(string text, bool visibleToCustomer, Guid staffId, DateTimeOffset now)
    {
        text = text?.Trim() ?? "";
        if (text.Length == 0)
            return WorkOrderErrors.NoteRequired;
        if (TooLong(text))
            return WorkOrderErrors.TextTooLong;

        var note = new WorkOrderNote(Id, text, visibleToCustomer, staffId, now);
        _notes.Add(note);
        return note;
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

    /// <summary>
    /// Onarım bitti: teklifteki stok parçaları şubenin stoğundan düşülür. Bir parça bile yetmezse
    /// iş emri onarımda kalır ve hiçbir miktar değişmez. Dönen hareketler kaydedilmelidir.
    /// </summary>
    public Result<IReadOnlyList<StockMovement>> MarkReady(Guid staffId, DateTimeOffset now, BranchStock stock)
    {
        if (!CanMoveTo(WorkOrderStatus.Ready))
            return WorkOrderErrors.InvalidTransition;
        if (stock.BranchId != BranchId)
            return InventoryErrors.WrongBranch;

        var movements = stock.Consume(StockParts(), Id, staffId, now);
        if (movements.IsFailure)
            return movements;

        MoveTo(WorkOrderStatus.Ready, staffId, now, null);
        PartsConsumed = true;
        return movements;
    }

    /// <summary>
    /// Teslim. Kalan borç varsa <paramref name="allowBalanceDue"/> gerekir; bu onayı sadece atölye sahibinin
    /// verebileceğini uygulama katmanı denetler. Borçlu teslimde o anki borç <see cref="BalanceAtDelivery"/>'de kalır.
    /// </summary>
    public Result Deliver(Guid staffId, DateTimeOffset now, int warrantyDays, bool allowBalanceDue = false)
    {
        if (!CanMoveTo(WorkOrderStatus.Delivered))
            return WorkOrderErrors.InvalidTransition;
        if (warrantyDays < 0 || warrantyDays > MaxWarrantyDays)
            return WorkOrderErrors.InvalidWarrantyDays;
        if (Balance > 0 && !allowBalanceDue)
            return PaymentErrors.BalanceDue;
        // Eksi bakiye: müşteriden fazla para alınmış (ör. kapora > inceleme ücreti); önce iade edilir.
        if (Balance < 0)
            return PaymentErrors.RefundDue;

        // Hazır'dan teslim = onarıldı. Reddedildi'den teslim = onarılmadan iade; garanti başlamaz.
        var repaired = Status == WorkOrderStatus.Ready;

        MoveTo(WorkOrderStatus.Delivered, staffId, now, null);
        DeliveredAt = now;
        BalanceAtDelivery = Balance > 0 ? Balance : null;
        WarrantyUntil = repaired && warrantyDays > 0 ? now.AddDays(warrantyDays) : null;
        // Cihaz şifresi sadece onarım süresince gerekir; teslimde saklanmaya devam etmez.
        Passcode = null;
        return Result.Success();
    }

    public Result<Payment> RecordPayment(decimal amount, PaymentMethod method, Guid staffId, DateTimeOffset now, string? note)
    {
        var error = CheckPayment(amount, note);
        if (error is not null)
            return error;
        if (amount > Balance)
            return PaymentErrors.Overpayment;

        var payment = new Payment(Id, PaymentKind.Payment, amount, method, staffId, now, Clean(note));
        _payments.Add(payment);
        return payment;
    }

    /// <summary>Müşteriye para iadesi (ör. iptal edilen işin kaporası). Alınandan fazlası iade edilemez.</summary>
    public Result<Payment> RecordRefund(decimal amount, PaymentMethod method, Guid staffId, DateTimeOffset now, string? note)
    {
        var error = CheckPayment(amount, note);
        if (error is not null)
            return error;
        if (amount > PaidTotal)
            return PaymentErrors.RefundTooLarge;

        var refund = new Payment(Id, PaymentKind.Refund, amount, method, staffId, now, Clean(note));
        _payments.Add(refund);
        return refund;
    }

    /// <summary>Teklif reddedilen cihaz için inceleme ücreti (KDV dahil); teslimden önce girilir.</summary>
    public Result SetDiagnosisFee(decimal fee)
    {
        if (Status != WorkOrderStatus.Declined)
            return WorkOrderErrors.InvalidTransition;
        if (fee < 0 || fee > QuoteLine.MaxUnitPrice || decimal.Round(fee, 2) != fee)
            return PaymentErrors.InvalidDiagnosisFee;

        DiagnosisFee = fee;
        return Result.Success();
    }

    private Error? CheckPayment(decimal amount, string? note)
    {
        if (Status == WorkOrderStatus.Cancelled)
            return PaymentErrors.NotAllowed;
        if (amount <= 0 || amount > QuoteLine.MaxUnitPrice || decimal.Round(amount, 2) != amount)
            return PaymentErrors.InvalidAmount;
        if (note?.Length > Payment.MaxNoteLength)
            return PaymentErrors.NoteTooLong;
        return null;
    }

    /// <summary>İptal. Parçalar daha önce stoktan düşüldüyse (iş emri hazırdı) stoğa geri döner.</summary>
    public Result<IReadOnlyList<StockMovement>> Cancel(Guid staffId, DateTimeOffset now, string reason, BranchStock stock)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0)
            return WorkOrderErrors.CancelReasonRequired;
        if (TooLong(reason))
            return WorkOrderErrors.TextTooLong;
        if (!CanMoveTo(WorkOrderStatus.Cancelled))
            return WorkOrderErrors.InvalidTransition;
        // Alınan para önce iade edilmeli; yoksa kasa kaydı iptal edilmiş işte para gösterirdi.
        if (PaidTotal > 0)
            return PaymentErrors.RefundRequired;
        if (stock.BranchId != BranchId)
            return InventoryErrors.WrongBranch;

        IReadOnlyList<StockMovement> movements = PartsConsumed ? stock.Return(StockParts(), Id, staffId, now) : [];
        MoveTo(WorkOrderStatus.Cancelled, staffId, now, reason);
        PartsConsumed = false;
        Passcode = null;
        return Result.Success(movements);
    }

    /// <summary>Teklifteki stok parçaları, parça başına toplam adet.</summary>
    private Dictionary<Guid, int> StockParts() =>
        _quoteLines
            .Where(l => l.PartId is not null)
            .GroupBy(l => l.PartId!.Value)
            .ToDictionary(g => g.Key, g => (int)g.Sum(l => l.Quantity));

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
