using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Customers;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Web.Models.WorkOrders;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Cihaz kabulü ve iş emri sayfası. Kabul iki adım, JavaScript'siz: önce telefonla müşteri aranır (GET),
/// sonra kayıtlı müşterinin bilgileriyle ya da yeni müşteri alanlarıyla form gönderilir (POST).
/// </summary>
[Authorize]
[Route("work-orders")]
public sealed class WorkOrdersController : Controller
{
    private const string Prefix = nameof(IntakePage.Input) + ".";

    // Kullanım senaryosunun hata kodu → formdaki alan ve metin.
    private static readonly Dictionary<string, (string Field, string Message)> Errors = new()
    {
        [CustomerErrors.InvalidPhone.Code] = (nameof(IntakeInput.Phone), "Telefonu 0532 123 45 67 gibi yaz."),
        [CustomerErrors.InvalidName.Code] = (nameof(IntakeInput.CustomerName), "Müşterinin adını yaz (2 ile 100 karakter)."),
        [CustomerErrors.InvalidEmail.Code] = (nameof(IntakeInput.CustomerEmail), "Geçerli bir e-posta yaz ya da boş bırak."),
        [CustomerErrors.ConsentRequired.Code] = (nameof(IntakeInput.KvkkConsent),
            "Aydınlatma metni müşteriye okunup onayı alınmadan yeni müşteri kaydedilemez."),
        [CustomerErrors.InvalidKind.Code] = (nameof(IntakeInput.DeviceKind), "Listeden bir cihaz türü seç."),
        [CustomerErrors.InvalidBrand.Code] = (nameof(IntakeInput.Brand), "Cihazın markasını yaz."),
        [CustomerErrors.InvalidModel.Code] = (nameof(IntakeInput.Model), "Cihazın modelini yaz."),
        [CustomerErrors.InvalidSerial.Code] = (nameof(IntakeInput.Serial), "Seri no en fazla 60 karakter olabilir."),
        [CustomerErrors.InvalidImei.Code] = (nameof(IntakeInput.Imei),
            "IMEI 15 rakam olmalı ve son rakamı tutmalı. Telefonda *#06# tuşlayınca görünür; emin değilsen boş bırak."),
        [WorkOrderErrors.ComplaintRequired.Code] = (nameof(IntakeInput.Complaint), "Müşterinin şikâyetini yaz."),
        [WorkOrderErrors.TextTooLong.Code] = ("", "Metin alanları en fazla 1000 karakter olabilir."),
        [WorkOrderErrors.InvalidPromisedDate.Code] = (nameof(IntakeInput.PromisedDate),
            $"Teslim günü bugün ya da sonraki {WorkOrder.MaxPromiseDays} gün içinde olmalı."),
        [ApplicationErrors.CustomerNotFound.Code] = ("", "Seçilen müşteri bulunamadı. Telefonla yeniden ara."),
        [ApplicationErrors.DeviceNotFound.Code] = ("", "Seçilen cihaz bu müşterinin değil. Listeden seç ya da yeni cihaz ekle."),
        [ApplicationErrors.DuplicatePhone.Code] = ("",
            "Bu telefonla kayıtlı bir müşteri var. Telefonla yeniden arayıp kayıtlı müşteriyi seç."),
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
            success: result.IsSuccess ? (input.Date is null ? "Teslim günü kaldırıldı." : "Teslim günü kaydedildi; takip sayfasında görünür.") : null);
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
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? $"{input.Amount:C} tahsilat kaydedildi." : null);
    }

    /// <summary>Para iadesi: kasadan çıkış olduğu için sadece sahip (senaryo denetler).</summary>
    [HttpPost("{id:guid}/refunds")]
    public async Task<IActionResult> Refund(Guid id, PaymentInput input, [FromServices] RecordRefund refund, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await refund.HandleAsync(new PaymentCommand(id, input.Amount, input.Method, input.Note), ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? $"{input.Amount:C} iade kaydedildi." : null);
    }

    /// <summary>Teklif reddedildiyse cihaz onarılmadan iade edilir; varsa inceleme ücreti.</summary>
    [HttpPost("{id:guid}/diagnosis-fee")]
    public async Task<IActionResult> DiagnosisFee(Guid id, decimal fee, [FromServices] SetDiagnosisFee set, [FromServices] GetWorkOrder get, CancellationToken ct)
    {
        Result result = await set.HandleAsync(id, fee, ct);
        return await AfterChangeAsync(id, result, pressed: false, get, ct, result.IsSuccess ? "İnceleme ücreti kaydedildi." : null);
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

    private static readonly Dictionary<string, string> StepMessages = new()
    {
        [WorkOrderErrors.InvalidTransition.Code] = "Bu işlem iş emrinin şu anki durumunda yapılamaz; sayfa güncellendi.",
        [WorkOrderErrors.CancelReasonRequired.Code] = "İptalin nedenini yaz; müşteriye ve geçmişe bu yazılır.",
        [WorkOrderErrors.NoteRequired.Code] = "Notu yaz.",
        [WorkOrderErrors.TextTooLong.Code] = "Metin en fazla 1000 karakter olabilir.",
        [WorkOrderErrors.InvalidPromisedDate.Code] = $"Teslim günü bugün ya da sonraki {WorkOrder.MaxPromiseDays} gün içinde olmalı.",
        [WorkOrderErrors.Closed.Code] = "Kapanmış iş emri değiştirilemez.",
        [QuoteErrors.Empty.Code] = "Teklifi göndermek için önce teklife kalem ekle.",
        [QuoteErrors.DeciderNameRequired.Code] = "Kararı veren kişinin adını yaz.",
        [InventoryErrors.InsufficientStock.Code] = "Teklifteki parçalardan stokta yeterince yok. Önce stok girişi yap, sonra tekrar dene.",
        [InventoryErrors.WrongBranch.Code] = "Bu iş emri başka bir şubenin; o şubeye geçip tekrar dene.",
        [PaymentErrors.BalanceDue.Code] = "Kalan borç var. Önce tahsilatı kaydet; borçlu teslimi sadece atölye sahibi onaylayabilir.",
        [PaymentErrors.RefundDue.Code] = "Müşteriden fazla ödeme alınmış; teslimden önce iadeyi kaydet.",
        [PaymentErrors.RefundRequired.Code] = "İptalden önce alınan ödemeyi iade et.",
        [ApplicationErrors.OwnerOnly.Code] = "Bu işlem sadece atölye sahibinin: para iadesi ve borçlu teslim.",
        [PaymentErrors.InvalidAmount.Code] = "Tutar sıfırdan büyük olmalı (en fazla iki ondalık).",
        [PaymentErrors.Overpayment.Code] = "Kalan borçtan fazla tahsilat alınamaz.",
        [PaymentErrors.RefundTooLarge.Code] = "Alınan paradan fazlası iade edilemez.",
        [PaymentErrors.NotAllowed.Code] = "İptal edilen iş emrine tahsilat girilemez.",
        [PaymentErrors.NoteTooLong.Code] = "Not en fazla 200 karakter olabilir.",
        [PaymentErrors.InvalidDiagnosisFee.Code] = "İnceleme ücreti sıfır ya da daha büyük, en fazla iki ondalık olmalı.",
        [QuoteErrors.InvalidDescription.Code] = "Kalemin açıklamasını yaz (en fazla 200 karakter).",
        [QuoteErrors.InvalidQuantity.Code] = "Miktar sıfırdan büyük bir sayı olmalı (en fazla iki ondalık).",
        [QuoteErrors.PartQuantityMustBeWhole.Code] = "Stoktan parça tam adet eklenir (1, 2, 3...).",
        [QuoteErrors.InvalidUnitPrice.Code] = "Birim fiyatı yaz: sıfır ya da daha büyük, en fazla iki ondalık.",
        [QuoteErrors.InvalidVatRate.Code] = "KDV oranı 0 ile 100 arasında olmalı.",
        [QuoteErrors.InvalidDiscount.Code] = "İskonto 0 ile 100 arasında olmalı.",
        [QuoteErrors.NotEditable.Code] = "Müşteri karar verdikten sonra teklif değiştirilemez.",
        [QuoteErrors.LineNotFound.Code] = "Bu kalem bulunamadı; sayfa güncellendi.",
        [ApplicationErrors.PartNotFound.Code] = "Seçilen parça bulunamadı ya da kullanımdan kalkmış.",
    };

    private static string StepMessage(Error error) =>
        StepMessages.GetValueOrDefault(error.Code, "İşlem yapılamadı. Sayfayı yenileyip tekrar dene.");

    private void AddError(Error error)
    {
        var (field, message) = Errors.GetValueOrDefault(error.Code, ("", "İş emri açılamadı. Bilgileri kontrol edip tekrar dene."));
        ModelState.AddModelError(field.Length == 0 ? "" : Prefix + field, message);
    }
}
