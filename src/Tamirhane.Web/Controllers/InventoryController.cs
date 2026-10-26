using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Inventory;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Inventory;
using Tamirhane.Web.Models.Inventory;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Stok: parça listesi (arama, kritik stok), parça sayfası (stok girişi, sayım düzeltmesi, hareketler).
/// Parça ekleme ve düzenleme sahibin işi; stok girişi ve sayımı teknisyen de yapar. Yetkiyi senaryolar denetler.
/// </summary>
[Authorize]
[Route("inventory")]
public sealed class InventoryController : Controller
{
    private static readonly Dictionary<string, (string Field, string Message)> Errors = new()
    {
        [InventoryErrors.InvalidSku.Code] = (nameof(PartInput.Sku), "Kod 1 ile 40 karakter arasında olmalı."),
        [InventoryErrors.InvalidName.Code] = (nameof(PartInput.Name), "Parçanın adını yaz (en fazla 200 karakter)."),
        [InventoryErrors.InvalidPrice.Code] = (nameof(PartInput.SalePrice), "Fiyatlar sıfır ya da daha büyük, en fazla iki ondalık olmalı."),
        [InventoryErrors.InvalidCriticalLevel.Code] = (nameof(PartInput.CriticalLevel), "Kritik seviye 0 ya da daha büyük olmalı."),
        [InventoryErrors.InvalidQuantity.Code] = ("", "Adet geçersiz: girişte sıfırdan büyük olmalı, sayımda şimdiki adetten farklı olmalı."),
        [InventoryErrors.ReasonRequired.Code] = ("", "Sayım düzeltmesinin nedenini yaz (ör. Sayım farkı, Kırıldı)."),
        [InventoryErrors.NoteTooLong.Code] = ("", "Not en fazla 200 karakter olabilir."),
        [ApplicationErrors.DuplicateSku.Code] = (nameof(PartInput.Sku), "Bu kodla bir parça zaten var."),
        [ApplicationErrors.OwnerOnly.Code] = ("", "Parça ekleme ve düzenleme sadece atölye sahibinin işi."),
    };

    [HttpGet("")]
    public Task<IActionResult> Index(string? q, bool low, bool inactive, [FromServices] ListParts list, CancellationToken ct) =>
        IndexAsync(q, low, inactive, new PartInput(), list, ct);

    [HttpPost("")]
    public async Task<IActionResult> Add(
        [Bind(Prefix = nameof(InventoryPage.NewPart))] PartInput input,
        [FromServices] AddPart add, [FromServices] ListParts list, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await add.HandleAsync(Command(input), input.OpeningQuantity, ct);
            if (result.IsSuccess)
            {
                TempData["Status"] = $"{input.Name.Trim()} eklendi.";
                return RedirectToAction(nameof(Part), new { id = result.Value });
            }
            AddError(result.Error!, nameof(InventoryPage.NewPart));
        }
        return await IndexAsync(null, false, false, input, list, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Part(Guid id, [FromServices] GetPart get, CancellationToken ct)
    {
        var detail = await get.HandleAsync(id, ct);
        if (detail.IsFailure)
            return NotFound();
        var p = detail.Value.Part;
        return View(new PartPage(detail.Value, new PartInput
        {
            Sku = p.Sku, Name = p.Name, CostPrice = p.CostPrice ?? 0, SalePrice = p.SalePrice, CriticalLevel = p.CriticalLevel,
        }));
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [Bind(Prefix = nameof(PartPage.Edit))] PartInput input,
        [FromServices] UpdatePart update, [FromServices] GetPart get, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await update.HandleAsync(id, Command(input), ct);
            if (result.IsSuccess)
                return Done(id, "Parça bilgileri kaydedildi.");
            if (result.Error == ApplicationErrors.PartNotFound)
                return NotFound();
            AddError(result.Error!, nameof(PartPage.Edit));
        }
        var detail = await get.HandleAsync(id, ct);
        return detail.IsFailure ? NotFound() : View(nameof(Part), new PartPage(detail.Value, input));
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, int quantity, string? note, [FromServices] ChangeStock change, CancellationToken ct) =>
        After(id, await change.ReceiveAsync(id, quantity, note, ct), $"{quantity} adet stoğa girdi.");

    [HttpPost("{id:guid}/adjust")]
    public async Task<IActionResult> Adjust(Guid id, int counted, string? reason, [FromServices] ChangeStock change, CancellationToken ct) =>
        After(id, await change.AdjustAsync(id, counted, reason ?? "", ct), $"Stok sayıma göre {counted} adet olarak düzeltildi.");

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, bool active, [FromServices] SetPartActive set, CancellationToken ct) =>
        After(id, await set.HandleAsync(id, active, ct), active ? "Parça yeniden kullanımda." : "Parça kullanımdan kaldırıldı; geçmiş kayıtları duruyor.");

    private async Task<IActionResult> IndexAsync(string? q, bool low, bool inactive, PartInput newPart, ListParts list, CancellationToken ct) =>
        View(nameof(Index), new InventoryPage(await list.HandleAsync(q, low, inactive, ct), q, low, inactive, newPart));

    private IActionResult After(Guid id, Result result, string success)
    {
        if (result.Error == ApplicationErrors.PartNotFound)
            return NotFound();
        if (result.IsSuccess)
            return Done(id, success);
        TempData["Error"] = Errors.GetValueOrDefault(result.Error!.Code, (Field: "", Message: "İşlem yapılamadı. Bilgileri kontrol edip tekrar dene.")).Message;
        return RedirectToAction(nameof(Part), new { id });
    }

    private IActionResult Done(Guid id, string message)
    {
        TempData["Status"] = message;
        return RedirectToAction(nameof(Part), new { id });
    }

    private void AddError(Error error, string prefix)
    {
        var (field, message) = Errors.GetValueOrDefault(error.Code, ("", "İşlem yapılamadı. Bilgileri kontrol edip tekrar dene."));
        ModelState.AddModelError(field.Length == 0 ? "" : $"{prefix}.{field}", message);
    }

    private static PartCommand Command(PartInput input) =>
        new(input.Sku, input.Name, input.CostPrice, input.SalePrice, input.CriticalLevel);
}
