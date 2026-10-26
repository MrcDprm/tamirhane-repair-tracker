using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.Models.Settings;

// Uzunluk ve aralık sınırları kabaca; asıl kurallar domain'de (Shop, Branch), hata metinleri SettingsController'da.

public sealed class ShopSettingsInput
{
    [Display(Name = "Atölyenin adı")]
    [Required(ErrorMessage = "Atölyenin adını yaz.")]
    [StringLength(100, ErrorMessage = "Atölye adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Garanti süresi (gün)")]
    [Range(0, 3650, ErrorMessage = "Garanti süresi 0 ile 3650 gün arasında olmalı.")]
    public int WarrantyDays { get; set; }

    [Display(Name = "Varsayılan KDV oranı (%)")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "KDV oranı 0 ile 100 arasında olmalı.")]
    public decimal DefaultVatRate { get; set; }

    [Display(Name = "Müşterilere giden yazıların dili")]
    [Required(ErrorMessage = "Bir dil seç.")]
    public string Locale { get; set; } = "tr";
}

public sealed class BranchInput
{
    [Display(Name = "Şube adı")]
    [Required(ErrorMessage = "Şubenin adını yaz.")]
    [StringLength(100, ErrorMessage = "Şube adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Adres")]
    [StringLength(300, ErrorMessage = "Adres en fazla 300 karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Telefon")]
    [StringLength(30, ErrorMessage = "Telefonu 0532 123 45 67 gibi yaz.")]
    public string? Phone { get; set; }
}

public sealed record SettingsPage(ShopSettingsInput Shop, IReadOnlyList<BranchItem> Branches, BranchInput NewBranch);
