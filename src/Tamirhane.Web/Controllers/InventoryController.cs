using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Inventory;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Inventory;
using Tamirhane.Web.Localization;
using Tamirhane.Web.Models.Inventory;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Stok: parça listesi (arama, kritik stok), parça sayfası (stok girişi, sayım düzeltmesi, hareketler).
/// Parça ekleme ve düzenleme sahibin işi; stok girişi ve sayımı teknisyen de yapar. Yetkiyi senaryolar denetler.
/// </summary>
[Authorize]
[Route("inventory")]
public sealed class InventoryController(IStringLocalizer<SharedResource> t) : Controller
{
    // Hata kodu → formdaki alan ve metnin anahtarı.
    private static readonly Dictionary<string, (string Field, string Key)> Errors = new()
    {
        [InventoryErrors.InvalidSku.Code] = (nameof(PartInput.Sku), "error.sku"),
        [InventoryErrors.InvalidName.Code] = (nameof(PartInput.Name), "error.part_name"),
        [InventoryErrors.InvalidPrice.Code] = (nameof(PartInput.SalePrice), "error.price"),
        [InventoryErrors.InvalidCriticalLevel.Code] = (nameof(PartInput.CriticalLevel), "error.critical_level"),
        [InventoryErrors.InvalidQuantity.Code] = ("", "error.stock_quantity"),
        [InventoryErrors.ReasonRequired.Code] = ("", "error.adjust_reason"),
        [InventoryErrors.NoteTooLong.Code] = ("", "error.payment_note_too_long"),
        [ApplicationErrors.DuplicateSku.Code] = (nameof(PartInput.Sku), "error.duplicate_sku"),
        [ApplicationErrors.OwnerOnly.Code] = ("", "error.parts_owner_only"),
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
                TempData["Status"] = t["inventory.added", input.Name.Trim()].Value;
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
                return Done(id, t["inventory.saved"]);
            if (result.Error == ApplicationErrors.PartNotFound)
                return NotFound();
            AddError(result.Error!, nameof(PartPage.Edit));
        }
        var detail = await get.HandleAsync(id, ct);
        return detail.IsFailure ? NotFound() : View(nameof(Part), new PartPage(detail.Value, input));
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, int quantity, string? note, [FromServices] ChangeStock change, CancellationToken ct) =>
        After(id, await change.ReceiveAsync(id, quantity, note, ct), t["inventory.received", quantity]);

    [HttpPost("{id:guid}/adjust")]
    public async Task<IActionResult> Adjust(Guid id, int counted, string? reason, [FromServices] ChangeStock change, CancellationToken ct) =>
        After(id, await change.AdjustAsync(id, counted, reason ?? "", ct), t["inventory.adjusted", counted]);

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, bool active, [FromServices] SetPartActive set, CancellationToken ct) =>
        After(id, await set.HandleAsync(id, active, ct), t[active ? "inventory.reactivated" : "inventory.retired"]);

    private async Task<IActionResult> IndexAsync(string? q, bool low, bool inactive, PartInput newPart, ListParts list, CancellationToken ct) =>
        View(nameof(Index), new InventoryPage(await list.HandleAsync(q, low, inactive, ct), q, low, inactive, newPart));

    private IActionResult After(Guid id, Result result, string success)
    {
        if (result.Error == ApplicationErrors.PartNotFound)
            return NotFound();
        if (result.IsSuccess)
            return Done(id, success);
        TempData["Error"] = t[Errors.GetValueOrDefault(result.Error!.Code, (Field: "", Key: "error.action_failed")).Key].Value;
        return RedirectToAction(nameof(Part), new { id });
    }

    private IActionResult Done(Guid id, string message)
    {
        TempData["Status"] = message;
        return RedirectToAction(nameof(Part), new { id });
    }

    private void AddError(Error error, string prefix)
    {
        var (field, key) = Errors.GetValueOrDefault(error.Code, ("", "error.action_failed"));
        ModelState.AddModelError(field.Length == 0 ? "" : $"{prefix}.{field}", t[key]);
    }

    private static PartCommand Command(PartInput input) =>
        new(input.Sku, input.Name, input.CostPrice, input.SalePrice, input.CriticalLevel);
}
