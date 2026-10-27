using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
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
/// <summary>Başka bir iş emrine link: garanti dönüşünde önceki iş emri ve önceki iş emrinde dönüşleri.</summary>
public sealed record WorkOrderLink(Guid Id, string Number);

public sealed record StatusStep(WorkOrderStatus Status, DateTimeOffset At, string? By, string? Note);

public sealed record NoteView(string Text, bool VisibleToCustomer, string By, DateTimeOffset At);

/// <summary>Teklif kalemi; tutarlar domain'de hesaplanmış hâliyle (formdan gelen toplam kullanılmaz).</summary>
public sealed record QuoteLineView(
    Guid Id, QuoteLineKind Kind, bool FromStock, string Description, decimal Quantity, decimal UnitPrice,
    decimal DiscountRate, decimal VatRate, decimal Total);

/// <summary>Kasaya giren ya da çıkan para (para sistemden geçmez; bu sadece kayıt).</summary>
public sealed record PaymentView(PaymentKind Kind, decimal Amount, PaymentMethod Method, DateTimeOffset At, string By, string? Note);

/// <summary>Teklife eklenebilecek stok parçası: personelin şubesindeki adediyle.</summary>
public sealed record QuotePartOption(Guid Id, string Sku, string Name, decimal SalePrice, int Quantity);

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
    public DateOnly? PromisedDate { get; init; }
    /// <summary>Bu iş emri bir garanti dönüşüyse önceki iş emri.</summary>
    public WorkOrderLink? WarrantyOf { get; init; }
    /// <summary>Bu iş emrinin garanti kapsamında geri gelmiş kayıtları.</summary>
    public IReadOnlyList<WorkOrderLink> WarrantyReturns { get; init; } = [];
    /// <summary>Teslim edilmiş ve garantisi süren iş emrinden "Garanti kapsamında yeni kayıt" açılabilir.</summary>
    public bool CanReopenUnderWarranty { get; init; }
    public bool IsWarrantyReturn => WarrantyOf is not null;
    /// <summary>"WhatsApp'tan bildir": müşterinin sohbetini o anki durumun hazır mesajı ve takip linkiyle açar.</summary>
    public Uri? WhatsAppLink { get; init; }
    /// <summary>Söz verilen gün değiştirilebilir mi (iş emri açıkken).</summary>
    public bool CanPromise => Status is not (WorkOrderStatus.Delivered or WorkOrderStatus.Cancelled);
    public IReadOnlyList<QuoteLineView> QuoteLines { get; init; } = [];
    public QuoteTotals QuoteTotals { get; init; } = QuoteTotals.Of([]);
    /// <summary>Kalem eklenip çıkarılabilir mi (inceleme ya da kararsız onarım sırasında).</summary>
    public bool IsQuoteEditable { get; init; }
    /// <summary>Kalem ekleme formu için; teklif düzenlenemiyorsa boş.</summary>
    public IReadOnlyList<QuotePartOption> PartOptions { get; init; } = [];
    public decimal DefaultVatRate { get; init; }
    public QuoteDecision? QuoteDecision { get; init; }
    public IReadOnlyList<PaymentView> Payments { get; init; } = [];
    public decimal PaidTotal { get; init; }
    public decimal DiagnosisFee { get; init; }
    /// <summary>Borçlu teslim edildiyse teslim anındaki borç.</summary>
    public decimal? BalanceAtDelivery { get; init; }
    /// <summary>İptal edilen işe tahsilat girilmez; borç kalmadıysa form gösterilmez.</summary>
    public bool CanTakePayment => Status != WorkOrderStatus.Cancelled && Balance > 0;
    /// <summary>İade: alınmış para varsa (formu sadece sahip görür; senaryo da denetler).</summary>
    public bool CanRefund => PaidTotal > 0;
    /// <summary>Teklif reddedildi, cihaz henüz teslim edilmedi: inceleme ücreti girilebilir.</summary>
    public bool CanSetDiagnosisFee => Status == WorkOrderStatus.Declined;

    public WorkOrderAction? NextAction => Actions.Count > 0 && Actions[0] != WorkOrderAction.Cancel ? Actions[0] : null;
}

public sealed class GetWorkOrder(
    IWorkOrderRepository orders, ICustomerRepository customers, IDeviceRepository devices, IStaffRepository staff,
    IPartRepository parts, IShopSettings settings, ICurrentUser user, ITrackingLinks links, TimeProvider clock)
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
        var names = await staff.NamesAsync(order.History.Select(h => h.StaffId).OfType<Guid>()
            .Concat(order.Notes.Select(n => n.StaffId)).Concat(order.Payments.Select(p => p.StaffId)), ct);
        var editable = order.IsQuoteEditable;
        var shop = await settings.GetAsync(user.ShopId, ct);
        // İki yönlü bağ: garanti dönüşünde önceki iş emri, önceki iş emrinde dönüşler.
        var warrantyOf = order.WarrantyOfId is { } previousId
            && (await orders.NumbersAsync([previousId], ct)).TryGetValue(previousId, out var previousNumber)
            ? new WorkOrderLink(previousId, previousNumber) : null;
        var returns = order.Status == WorkOrderStatus.Delivered ? await orders.WarrantyReturnsAsync(order.Id, ct) : [];
        // Parça seçimi iş emrinin şubesinin stoğundan (onarım bitince stok oradan düşer).
        var options = editable
            ? (await parts.ListActiveWithStockAsync(order.BranchId, ct))
                .OrderBy(p => p.Part.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
                .Select(p => new QuotePartOption(p.Part.Id, p.Part.Sku, p.Part.Name, p.Part.SalePrice, p.Quantity)).ToList()
            : [];

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
            PromisedDate = order.PromisedDate,
            QuoteLines = order.QuoteLines.Select(l => new QuoteLineView(
                l.Id, l.Kind, l.PartId is not null, l.Description, l.Quantity, l.UnitPrice, l.DiscountRate, l.VatRate, l.Total)).ToList(),
            QuoteTotals = order.QuoteTotals,
            IsQuoteEditable = editable,
            PartOptions = options,
            DefaultVatRate = editable ? shop.DefaultVatRate : 0,
            QuoteDecision = order.QuoteDecision,
            Payments = order.Payments.OrderBy(p => p.At)
                .Select(p => new PaymentView(p.Kind, p.Amount, p.Method, p.At, names.GetValueOrDefault(p.StaffId, "?"), p.Note)).ToList(),
            PaidTotal = order.PaidTotal,
            DiagnosisFee = order.DiagnosisFee,
            BalanceAtDelivery = order.BalanceAtDelivery,
            WarrantyOf = warrantyOf,
            WarrantyReturns = returns,
            CanReopenUnderWarranty = order.Status == WorkOrderStatus.Delivered && order.IsUnderWarranty(TurkeyTime.Now(clock)),
            WhatsAppLink = WhatsAppMessage.Link(customer.Phone.Value, WhatsAppMessage.Text(
                customer.Name, $"{device.Brand} {device.Model}", order.Number, order.Status, shop.Locale, links.For(order.TrackingCode))),
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
