using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.Localization;
using Tamirhane.Web.Models.Settings;

namespace Tamirhane.Web.Controllers;

/// <summary>Atölye ayarları (ad, garanti, KDV, dil) ve şubeler. Sadece sahip.</summary>
[Authorize(Policy = Policies.OwnerOnly)]
[Route("settings")]
public sealed class SettingsController(ListBranches listBranches, IStringLocalizer<SharedResource> t) : Controller
{
    // Hata kodu → formdaki alan ve metnin anahtarı.
    private static readonly Dictionary<string, (string Field, string Key)> Errors = new()
    {
        [ShopErrors.InvalidName.Code] = ("Shop.Name", "account.error.shop_name"),
        [ShopErrors.InvalidWarrantyDays.Code] = ("Shop.WarrantyDays", "error.warranty_days"),
        [ShopErrors.InvalidVatRate.Code] = ("Shop.DefaultVatRate", "error.default_vat"),
        [ShopErrors.InvalidLocale.Code] = ("Shop.Locale", "error.locale"),
        [ShopErrors.InvalidBranchName.Code] = ("NewBranch.Name", "error.branch_name"),
        [ShopErrors.InvalidBranchAddress.Code] = ("NewBranch.Address", "error.branch_address"),
        [ShopErrors.InvalidBranchPhone.Code] = ("NewBranch.Phone", "error.phone"),
        [ShopErrors.TooManyBranches.Code] = ("", "error.too_many_branches"),
        [ShopErrors.BranchNotFound.Code] = ("", "error.branch_not_found"),
    };

    [HttpGet("")]
    public async Task<IActionResult> Index([FromServices] GetShopSettings get, CancellationToken ct)
    {
        var settings = await get.HandleAsync(ct);
        if (settings.IsFailure)
            return Forbid();
        var s = settings.Value;
        return await PageAsync(new ShopSettingsInput
        {
            Name = s.Name, WarrantyDays = s.WarrantyDays, DefaultVatRate = s.DefaultVatRate, Locale = s.Locale,
        }, new BranchInput(), ct);
    }

    [HttpPost("")]
    public async Task<IActionResult> Update(
        [Bind(Prefix = nameof(SettingsPage.Shop))] ShopSettingsInput input,
        [FromServices] UpdateShopSettings update, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await PageAsync(input, new BranchInput(), ct);

        var result = await update.HandleAsync(new ShopSettingsCommand(input.Name, input.WarrantyDays, input.DefaultVatRate, input.Locale), ct);
        if (result.IsFailure)
            return await FailAsync(result.Error!, input, new BranchInput(), ct);

        TempData["Status"] = t["settings.saved"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("branches")]
    public async Task<IActionResult> AddBranch(
        [Bind(Prefix = nameof(SettingsPage.NewBranch))] BranchInput input,
        [FromServices] AddBranch add, [FromServices] GetShopSettings get, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await PageAsync(await CurrentSettingsAsync(get, ct), input, ct);

        var result = await add.HandleAsync(new BranchCommand(input.Name, input.Address, input.Phone), ct);
        if (result.IsFailure)
            return await FailAsync(result.Error!, await CurrentSettingsAsync(get, ct), input, ct);

        TempData["Status"] = t["settings.branch_added", input.Name.Trim()].Value;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Şube satırındaki düzenleme formu; hata ayrı sayfada değil, sayfanın üstünde gösterilir.</summary>
    [HttpPost("branches/{id:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid id, BranchInput input, [FromServices] UpdateBranch update, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await update.HandleAsync(id, new BranchCommand(input.Name, input.Address, input.Phone), ct)
            : ShopErrors.InvalidBranchName;
        if (result.Error?.Kind == ErrorKind.Forbidden)
            return Forbid();

        TempData[result.IsSuccess ? "Status" : "Error"] = result.IsSuccess
            ? t["settings.branch_saved"].Value
            : Message(Errors.GetValueOrDefault(result.Error!.Code, (Field: "", Key: "error.branch_update"))).Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> PageAsync(ShopSettingsInput shop, BranchInput newBranch, CancellationToken ct) =>
        View(nameof(Index), new SettingsPage(shop, await listBranches.HandleAsync(ct), newBranch));

    private async Task<IActionResult> FailAsync(Error error, ShopSettingsInput shop, BranchInput newBranch, CancellationToken ct)
    {
        if (error.Kind == ErrorKind.Forbidden)
            return Forbid();
        var entry = Errors.GetValueOrDefault(error.Code, (Field: "", Key: "account.error.generic"));
        ModelState.AddModelError(entry.Field, Message(entry));
        return await PageAsync(shop, newBranch, ct);
    }

    // Yer tutuculu tek metin şube sınırı; diğerlerinde fazladan argüman yok sayılır.
    private LocalizedString Message((string Field, string Key) entry) => t[entry.Key, Branch.MaxPerShop];

    private static async Task<ShopSettingsInput> CurrentSettingsAsync(GetShopSettings get, CancellationToken ct)
    {
        var s = (await get.HandleAsync(ct)).Value;
        return new ShopSettingsInput { Name = s.Name, WarrantyDays = s.WarrantyDays, DefaultVatRate = s.DefaultVatRate, Locale = s.Locale };
    }
}
