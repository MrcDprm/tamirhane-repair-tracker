using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Customers;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Web.Localization;
using Tamirhane.Web.Models.WorkOrders;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Cihaz kabulü ve iş emri sayfası. Kabul iki adım, JavaScript'siz: önce telefonla müşteri aranır (GET),
/// sonra kayıtlı müşterinin bilgileriyle ya da yeni müşteri alanlarıyla form gönderilir (POST).
/// </summary>
[Authorize]
[Route("work-orders")]
public sealed class WorkOrdersController(IStringLocalizer<SharedResource> t) : Controller
{
    private const string Prefix = nameof(IntakePage.Input) + ".";

    // Kullanım senaryosunun hata kodu → formdaki alan ve metnin anahtarı (Resources/*.json).
    private static readonly Dictionary<string, (string Field, string Key)> Errors = new()
    {
        [CustomerErrors.InvalidPhone.Code] = (nameof(IntakeInput.Phone), "error.phone"),
        [CustomerErrors.InvalidName.Code] = (nameof(IntakeInput.CustomerName), "error.customer_name"),
        [CustomerErrors.InvalidEmail.Code] = (nameof(IntakeInput.CustomerEmail), "error.customer_email"),
        [CustomerErrors.ConsentRequired.Code] = (nameof(IntakeInput.KvkkConsent), "error.kvkk_consent"),
        [CustomerErrors.InvalidKind.Code] = (nameof(IntakeInput.DeviceKind), "error.device_kind"),
        [CustomerErrors.InvalidBrand.Code] = (nameof(IntakeInput.Brand), "error.brand"),
        [CustomerErrors.InvalidModel.Code] = (nameof(IntakeInput.Model), "error.model"),
        [CustomerErrors.InvalidSerial.Code] = (nameof(IntakeInput.Serial), "error.serial"),
        [CustomerErrors.InvalidImei.Code] = (nameof(IntakeInput.Imei), "error.imei"),
        [WorkOrderErrors.ComplaintRequired.Code] = (nameof(IntakeInput.Complaint), "validation.complaint.required"),
        [WorkOrderErrors.TextTooLong.Code] = ("", "error.text_fields_too_long"),
        [WorkOrderErrors.InvalidPromisedDate.Code] = (nameof(IntakeInput.PromisedDate), "error.promised_date"),
        [ApplicationErrors.CustomerNotFound.Code] = ("", "error.customer_not_found"),
        [ApplicationErrors.DeviceNotFound.Code] = ("", "error.device_not_found"),
        [ApplicationErrors.DuplicatePhone.Code] = ("", "error.duplicate_phone"),
    };

    /// <summary>
    /// İş emri listesi ve hızlı arama (üst çubuktaki arama buraya gelir). Arama tek sonuç verirse doğrudan o iş emri açılır:
    /// tezgâhta müşteri "cihazım ne oldu" dediğinde telefonu yazmak yeter.
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, WorkOrderFilter filter, [FromServices] ListWorkOrders list, CancellationToken ct)
    {
        // Arama yapılıyorsa varsayılan süzgeç "tümü": kapanmış işler de bulunsun.
        if (!string.IsNullOrWhiteSpace(q) && !Request.Query.ContainsKey("filter"))
            filter = WorkOrderFilter.All;
        var result = await list.HandleAsync(q, filter, ct);
        if (result.Query is { Length: > 0 } && result.Rows.Count == 1)
            return RedirectToAction(nameof(Details), new { id = result.Rows[0].Id });
        return View(result);
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(string? phone, [FromServices] FindCustomerByPhone find, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return View(new IntakePage(false, null, new IntakeInput()));

        var input = new IntakeInput { Phone = phone.Trim() };
        var lookup = await find.HandleAsync(phone, ct);
        if (lookup.IsFailure)
        {
            AddError(lookup.Error!);
            return View(new IntakePage(false, null, input));
        }

        input.CustomerId = lookup.Value?.Id;
        return View(new IntakePage(true, lookup.Value, input));
    }

    [HttpPost("new")]
    public async Task<IActionResult> New(
        [Bind(Prefix = nameof(IntakePage.Input))] IntakeInput input,
        [FromServices] FindCustomerByPhone find, [FromServices] ReceiveDevice receive, CancellationToken ct)
    {
        // Sayfa yeniden çizilirse müşterinin kayıtlı cihazları gerekir; telefon formda gizli alanla durur.
        var lookup = await find.HandleAsync(input.Phone, ct);
        if (lookup.IsFailure)
        {
            AddError(lookup.Error!);
            return View(new IntakePage(false, null, input));
        }
        var page = new IntakePage(true, lookup.Value, input);
        if (!ModelState.IsValid)
            return View(page);

        var result = await receive.HandleAsync(new ReceiveDeviceCommand(
            input.CustomerId, input.CustomerName, input.Phone, input.CustomerEmail, input.KvkkConsent,
            input.DeviceId, input.DeviceKind, input.Brand, input.Model, input.Serial, input.Imei,
            input.Complaint, input.Condition, input.Accessories, input.Passcode, input.PromisedDate), ct);
        if (result.IsFailure)
        {
            AddError(result.Error!);
            return View(page);
        }

        TempData["Created"] = true;
        return RedirectToAction(nameof(Details), new { id = result.Value.WorkOrderId });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        var order = await get.HandleAsync(id, ct);
        return order.IsFailure ? NotFound() : View(order.Value);
    }

    /// <summary>
    /// Garanti kapsamında yeni kayıt (#34): teslim edilmiş ve garantisi süren iş emrinden. Müşteri ve cihaz aynı kalır,
    /// yeni iş emri öncekine bağlanır; teklifte işçilik ücretsiz önerilir.
    /// </summary>
    [HttpGet("{id:guid}/warranty")]
    public async Task<IActionResult> Warranty(Guid id, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        var previous = await get.HandleAsync(id, ct);
        if (previous.IsFailure)
            return NotFound();
        if (!previous.Value.CanReopenUnderWarranty)
        {
            TempData["Error"] = t["error.warranty_ended"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(new WarrantyPage(previous.Value, new WarrantyInput()));
    }

    [HttpPost("{id:guid}/warranty")]
    public async Task<IActionResult> Warranty(
        Guid id, [Bind(Prefix = nameof(WarrantyPage.Input))] WarrantyInput input,
        [FromServices] GetWorkOrder get, [FromServices] ReopenUnderWarranty reopen, CancellationToken ct)
    {
        var previous = await get.HandleAsync(id, ct);
        if (previous.IsFailure)
            return NotFound();
        if (!ModelState.IsValid)
            return View(new WarrantyPage(previous.Value, input));

        var result = await reopen.HandleAsync(
            new ReopenUnderWarrantyCommand(id, input.Complaint, input.Condition, input.Accessories, input.Passcode), ct);
        if (result.IsFailure)
        {
            if (result.Error == ApplicationErrors.WorkOrderNotFound)
                return NotFound();
            ModelState.AddModelError("", result.Error == WorkOrderErrors.NotUnderWarranty ? t["error.warranty_ended"]
                : StepMessage(result.Error!));
            return View(new WarrantyPage(previous.Value, input));
        }

        TempData["Created"] = true;
        return RedirectToAction(nameof(Details), new { id = result.Value.WorkOrderId });
    }

    /// <summary>A5 kabul fişi: tarayıcının yazdırma özelliğiyle basılır (PDF kütüphanesi yok).</summary>
    [HttpGet("{id:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid id, [FromServices] GetReceipt get, CancellationToken ct)
    {
        var receipt = await get.HandleAsync(id, ct);
        return receipt.IsFailure ? NotFound() : View(receipt.Value);
    }

    /// <summary>Cihaza yapıştırılan küçük etiket: numara ve QR.</summary>
    [HttpGet("{id:guid}/label")]
    public async Task<IActionResult> Label(Guid id, [FromServices] GetReceipt get, CancellationToken ct)
    {
        var receipt = await get.HandleAsync(id, ct);
        return receipt.IsFailure ? NotFound() : View(receipt.Value);
    }

    /// <summary>
    /// Takip linkinin QR'ı, ayrı bir SVG dosyası olarak (img etiketiyle): sayfaya ham HTML gömülmez, CSP sıkı kalır.
    /// Sadece oturumdaki atölyenin iş emri için üretilir.
    /// </summary>
    [HttpGet("{id:guid}/qr.svg")]
    public async Task<IActionResult> Qr(Guid id, [FromServices] GetReceipt get, [FromServices] IQrCodeGenerator qr, CancellationToken ct)
    {
        var receipt = await get.HandleAsync(id, ct);
        if (receipt.IsFailure)
            return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return Content(qr.Svg(receipt.Value.TrackingUrl.AbsoluteUri), "image/svg+xml; charset=utf-8");
    }

    /// <summary>
    /// Durum geçişi. Hangi geçişin geçerli olduğuna domain karar verir; sayfa sadece geçerli olanları gösterir ama
    /// elle gönderilen geçersiz istek de reddedilir. htmx isteğinde sadece iş emri bölümü döner (sayfa yenilenmez),
    /// JavaScript yoksa sayfa yeniden yüklenir.
    /// </summary>
    [HttpPost("{id:guid}/steps")]
    public async Task<IActionResult> Step(Guid id, StepInput input, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        var services = HttpContext.RequestServices;
        Result result = input.Step switch
        {
            WorkOrderAction.StartDiagnosis => await services.GetRequiredService<StartDiagnosis>().HandleAsync(id, ct),
            WorkOrderAction.SendQuote => await services.GetRequiredService<SendQuote>().HandleAsync(id, ct),
            WorkOrderAction.StartRepair => await services.GetRequiredService<StartRepair>().HandleAsync(id, ct),
            WorkOrderAction.RecordApproval => await services.GetRequiredService<RecordQuoteDecision>().HandleAsync(id, true, input.DecidedBy ?? "", ct),
            WorkOrderAction.RecordDecline => await services.GetRequiredService<RecordQuoteDecision>().HandleAsync(id, false, input.DecidedBy ?? "", ct),
            WorkOrderAction.MarkReady => await services.GetRequiredService<MarkReady>().HandleAsync(id, ct),
            WorkOrderAction.Deliver => await services.GetRequiredService<DeliverWorkOrder>().HandleAsync(id, input.AllowBalanceDue, ct),
            WorkOrderAction.Cancel => await services.GetRequiredService<CancelWorkOrder>().HandleAsync(id, input.Reason ?? "", ct),
            _ => WorkOrderErrors.InvalidTransition,
        };
        return await AfterChangeAsync(id, result, pressed: true, get, ct);
    }

    /// <summary>Not: atölye notu sadece içeride, müşteriye görünen not takip sayfasında da çıkar.</summary>
    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> Note(Guid id, NoteInput input, [FromServices] AddNote add, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await add.HandleAsync(id, input.Text ?? "", input.VisibleToCustomer, ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct);
    }

    [HttpPost("{id:guid}/promise")]
    public async Task<IActionResult> Promise(Guid id, PromiseInput input, [FromServices] PromiseDelivery promise, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = ModelState.IsValid
            ? await promise.HandleAsync(id, input.Date, ct)
            : WorkOrderErrors.InvalidPromisedDate;
        return await AfterChangeAsync(id, result, pressed: false, get, ct,
            success: result.IsSuccess ? t[input.Date is null ? "workorder.promise_removed" : "workorder.promise_saved"].Value : null);
    }

    /// <summary>
    /// Teklif kalemi: stoktan parça ya da serbest kalem. Fiyat ve KDV boşsa sunucu doldurur (parçanın satış fiyatı,
    /// atölyenin varsayılan KDV'si); toplamlar her zaman sunucuda hesaplanır.
    /// </summary>
    [HttpPost("{id:guid}/quote-lines")]
    public async Task<IActionResult> AddLine(
        Guid id, QuoteLineInput input, [FromServices] AddQuoteLine add, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = ModelState.IsValid
            ? await add.HandleAsync(new AddQuoteLineCommand(id, input.Kind, input.Kind == QuoteLineKind.Part ? input.PartId : null,
                input.Description, input.Quantity, input.UnitPrice, input.VatRate, input.DiscountRate), ct)
            : QuoteErrors.InvalidQuantity;
        return await AfterChangeAsync(id, result, pressed: false, get, ct);
    }

    [HttpPost("{id:guid}/quote-lines/{lineId:guid}/remove")]
    public async Task<IActionResult> RemoveLine(
        Guid id, Guid lineId, [FromServices] RemoveQuoteLine remove, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await remove.HandleAsync(id, lineId, ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct);
    }

    /// <summary>Tahsilat: nakit, kart ya da havale; kısmi olabilir. Kalan borçtan fazlası alınamaz.</summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> Pay(Guid id, PaymentInput input, [FromServices] RecordPayment pay, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await pay.HandleAsync(new PaymentCommand(id, input.Amount, input.Method, input.Note), ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? t["payment.recorded", input.Amount].Value : null);
    }

    /// <summary>Para iadesi: kasadan çıkış olduğu için sadece sahip (senaryo denetler).</summary>
    [HttpPost("{id:guid}/refunds")]
    public async Task<IActionResult> Refund(Guid id, PaymentInput input, [FromServices] RecordRefund refund, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await refund.HandleAsync(new PaymentCommand(id, input.Amount, input.Method, input.Note), ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? t["payment.refunded", input.Amount].Value : null);
    }

    /// <summary>Teklif reddedildiyse cihaz onarılmadan iade edilir; varsa inceleme ücreti.</summary>
    [HttpPost("{id:guid}/diagnosis-fee")]
    public async Task<IActionResult> DiagnosisFee(Guid id, decimal fee, [FromServices] SetDiagnosisFee set, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await set.HandleAsync(id, fee, ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? t["payment.diagnosis_fee_saved"].Value : null);
    }

    private async Task<IActionResult> AfterChangeAsync(
        Guid id, Result result, bool pressed, GetWorkOrder get, CancellationToken ct, string? success = null)
    {
        if (result.Error == ApplicationErrors.WorkOrderNotFound)
            return NotFound();
        var error = result.IsFailure ? StepMessage(result.Error!) : null;

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var order = await get.HandleAsync(id, ct);
            ViewData["Pressed"] = pressed && result.IsSuccess;
            ViewData["Error"] = error;
            ViewData["Success"] = success;
            return PartialView("_WorkOrderBody", order.Value);
        }

        TempData[result.IsSuccess ? "Pressed" : "Error"] = result.IsSuccess ? pressed : error;
        if (success is not null)
            TempData["Success"] = success;
        return RedirectToAction(nameof(Details), new { id });
    }

    // Hata kodu → metnin anahtarı.
    private static readonly Dictionary<string, string> StepMessages = new()
    {
        [WorkOrderErrors.InvalidTransition.Code] = "error.invalid_transition",
        [WorkOrderErrors.CancelReasonRequired.Code] = "error.cancel_reason",
        [WorkOrderErrors.NoteRequired.Code] = "error.note_required",
        [WorkOrderErrors.TextTooLong.Code] = "error.text_too_long",
        [WorkOrderErrors.InvalidPromisedDate.Code] = "error.promised_date",
        [WorkOrderErrors.Closed.Code] = "error.closed",
        [QuoteErrors.Empty.Code] = "error.quote_empty",
        [QuoteErrors.DeciderNameRequired.Code] = "error.decider_name",
        [InventoryErrors.InsufficientStock.Code] = "error.insufficient_stock",
        [InventoryErrors.WrongBranch.Code] = "error.wrong_branch",
        [PaymentErrors.BalanceDue.Code] = "error.balance_due",
        [PaymentErrors.RefundDue.Code] = "error.refund_due",
        [PaymentErrors.RefundRequired.Code] = "error.refund_required",
        [ApplicationErrors.OwnerOnly.Code] = "error.owner_only",
        [PaymentErrors.InvalidAmount.Code] = "error.invalid_amount",
        [PaymentErrors.Overpayment.Code] = "error.overpayment",
        [PaymentErrors.RefundTooLarge.Code] = "error.refund_too_large",
        [PaymentErrors.NotAllowed.Code] = "error.payment_not_allowed",
        [PaymentErrors.NoteTooLong.Code] = "error.payment_note_too_long",
        [PaymentErrors.InvalidDiagnosisFee.Code] = "error.diagnosis_fee",
        [QuoteErrors.InvalidDescription.Code] = "error.line_description",
        [QuoteErrors.InvalidQuantity.Code] = "error.line_quantity",
        [QuoteErrors.PartQuantityMustBeWhole.Code] = "error.part_quantity_whole",
        [QuoteErrors.InvalidUnitPrice.Code] = "error.unit_price",
        [QuoteErrors.InvalidVatRate.Code] = "error.vat_rate",
        [QuoteErrors.InvalidDiscount.Code] = "error.discount",
        [QuoteErrors.NotEditable.Code] = "error.quote_locked",
        [QuoteErrors.LineNotFound.Code] = "error.line_not_found",
        [ApplicationErrors.PartNotFound.Code] = "error.part_not_found",
    };

    // Yer tutuculu tek mesaj teslim günü sınırı ({0}); diğer metinlerde fazladan argüman string.Format'ta yok sayılır.
    private string StepMessage(Error error) =>
        t[StepMessages.GetValueOrDefault(error.Code, "error.step_failed"), WorkOrder.MaxPromiseDays];

    private void AddError(Error error)
    {
        var (field, key) = Errors.GetValueOrDefault(error.Code, ("", "error.intake_failed"));
        ModelState.AddModelError(field.Length == 0 ? "" : Prefix + field, t[key, WorkOrder.MaxPromiseDays]);
    }
}
