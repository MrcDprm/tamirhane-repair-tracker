using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Customers;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
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
        [ApplicationErrors.CustomerNotFound.Code] = ("", "Seçilen müşteri bulunamadı. Telefonla yeniden ara."),
        [ApplicationErrors.DeviceNotFound.Code] = ("", "Seçilen cihaz bu müşterinin değil. Listeden seç ya da yeni cihaz ekle."),
        [ApplicationErrors.DuplicatePhone.Code] = ("",
            "Bu telefonla kayıtlı bir müşteri var. Telefonla yeniden arayıp kayıtlı müşteriyi seç."),
    };

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
            input.Complaint, input.Condition, input.Accessories, input.Passcode), ct);
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

    private void AddError(Error error)
    {
        var (field, message) = Errors.GetValueOrDefault(error.Code, ("", "İş emri açılamadı. Bilgileri kontrol edip tekrar dene."));
        ModelState.AddModelError(field.Length == 0 ? "" : Prefix + field, message);
    }
}
