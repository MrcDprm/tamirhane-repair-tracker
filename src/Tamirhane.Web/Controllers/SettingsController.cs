using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.Models.Settings;

namespace Tamirhane.Web.Controllers;

/// <summary>Atölye ayarları (ad, garanti, KDV, dil) ve şubeler. Sadece sahip.</summary>
[Authorize(Policy = Policies.OwnerOnly)]
[Route("settings")]
public sealed class SettingsController(ListBranches listBranches) : Controller
{
    private static readonly Dictionary<string, (string Field, string Message)> Errors = new()
    {
        [ShopErrors.InvalidName.Code] = ("Shop.Name", "Atölye adı 2 ile 100 karakter arasında olmalı."),
        [ShopErrors.InvalidWarrantyDays.Code] = ("Shop.WarrantyDays", "Garanti süresi 0 ile 3650 gün arasında olmalı."),
        [ShopErrors.InvalidVatRate.Code] = ("Shop.DefaultVatRate", "KDV oranı 0 ile 100 arasında, en fazla iki ondalıklı olmalı."),
        [ShopErrors.InvalidLocale.Code] = ("Shop.Locale", "Listeden bir dil seç."),
        [ShopErrors.InvalidBranchName.Code] = ("NewBranch.Name", "Şube adı 2 ile 100 karakter arasında olmalı."),
        [ShopErrors.InvalidBranchAddress.Code] = ("NewBranch.Address", "Adres en fazla 300 karakter olabilir."),
        [ShopErrors.InvalidBranchPhone.Code] = ("NewBranch.Phone", "Telefonu 0532 123 45 67 gibi yaz."),
        [ShopErrors.TooManyBranches.Code] = ("", $"Bir atölyenin en fazla {Branch.MaxPerShop} şubesi olabilir."),
        [ShopErrors.BranchNotFound.Code] = ("", "Bu şube bulunamadı."),
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

        TempData["Status"] = "Ayarlar kaydedildi. Garanti ve KDV yeni iş emirlerine ve yeni teklif kalemlerine uygulanır.";
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

        TempData["Status"] = $"{input.Name.Trim()} şubesi eklendi. Menüdeki şube seçiciyle o şubeye geçebilirsin.";
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
            ? "Şube güncellendi."
            : Errors.GetValueOrDefault(result.Error!.Code, (Field: "", Message: "Şube güncellenemedi.")).Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> PageAsync(ShopSettingsInput shop, BranchInput newBranch, CancellationToken ct) =>
        View(nameof(Index), new SettingsPage(shop, await listBranches.HandleAsync(ct), newBranch));

    private async Task<IActionResult> FailAsync(Error error, ShopSettingsInput shop, BranchInput newBranch, CancellationToken ct)
    {
        if (error.Kind == ErrorKind.Forbidden)
            return Forbid();
        var (field, message) = Errors.GetValueOrDefault(error.Code, ("", "İşlem tamamlanamadı. Bilgileri kontrol edip tekrar dene."));
        ModelState.AddModelError(field, message);
        return await PageAsync(shop, newBranch, ct);
    }

    private static async Task<ShopSettingsInput> CurrentSettingsAsync(GetShopSettings get, CancellationToken ct)
    {
        var s = (await get.HandleAsync(ct)).Value;
        return new ShopSettingsInput { Name = s.Name, WarrantyDays = s.WarrantyDays, DefaultVatRate = s.DefaultVatRate, Locale = s.Locale };
    }
}
