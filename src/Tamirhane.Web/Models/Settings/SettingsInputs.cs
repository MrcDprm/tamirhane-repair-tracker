using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.Models.Settings;

// Uzunluk ve aralık sınırları kabaca; asıl kurallar domain'de (Shop, Branch), hata metinleri SettingsController'da.
// Alan adları ve mesajlar Resources/*.json anahtarlarıdır (#35).

public sealed class ShopSettingsInput
{
    [Display(Name = "field.shop_name")]
    [Required(ErrorMessage = "validation.shop_name.required_panel")]
    [StringLength(100, ErrorMessage = "validation.shop_name.length")]
    public string Name { get; set; } = "";

    [Display(Name = "field.warranty_days")]
    [Range(0, 3650, ErrorMessage = "error.warranty_days")]
    public int WarrantyDays { get; set; }

    [Display(Name = "field.default_vat")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "error.vat_rate")]
    public decimal DefaultVatRate { get; set; }

    [Display(Name = "field.customer_language")]
    [Required(ErrorMessage = "error.locale")]
    public string Locale { get; set; } = "tr";
}

public sealed class BranchInput
{
    [Display(Name = "field.branch_name")]
    [Required(ErrorMessage = "validation.branch_name.required")]
    [StringLength(100, ErrorMessage = "validation.max_chars")]
    public string Name { get; set; } = "";

    [Display(Name = "field.address")]
    [StringLength(300, ErrorMessage = "validation.max_chars")]
    public string? Address { get; set; }

    [Display(Name = "field.phone")]
    [StringLength(30, ErrorMessage = "error.phone")]
    public string? Phone { get; set; }
}

public sealed record SettingsPage(ShopSettingsInput Shop, IReadOnlyList<BranchItem> Branches, BranchInput NewBranch);
