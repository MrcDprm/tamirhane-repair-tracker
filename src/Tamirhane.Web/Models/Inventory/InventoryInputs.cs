using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Inventory;

namespace Tamirhane.Web.Models.Inventory;

// Uzunluk ve aralık sınırları kabaca; asıl kurallar domain'de (Part, StockLevel), hata metinleri InventoryController'da.

public sealed class PartInput
{
    [Display(Name = "Kod")]
    [Required(ErrorMessage = "Parçanın kodunu yaz (ör. IP13-SCR).")]
    [StringLength(40, ErrorMessage = "Kod en fazla 40 karakter olabilir.")]
    public string Sku { get; set; } = "";

    [Display(Name = "Parçanın adı")]
    [Required(ErrorMessage = "Parçanın adını yaz.")]
    [StringLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Alış fiyatı")]
    public decimal CostPrice { get; set; }

    [Display(Name = "Satış fiyatı")]
    public decimal SalePrice { get; set; }

    [Display(Name = "Kritik seviye")]
    [Range(0, 1_000_000, ErrorMessage = "Kritik seviye 0 ya da daha büyük olmalı.")]
    public int CriticalLevel { get; set; }

    /// <summary>Sadece yeni parçada: elde olan adet, "alış" olarak girer.</summary>
    [Display(Name = "Eldeki adet")]
    [Range(0, 1_000_000, ErrorMessage = "Adet 0 ya da daha büyük olmalı.")]
    public int OpeningQuantity { get; set; }
}

public sealed record InventoryPage(PartList List, string? Query, bool LowOnly, bool IncludeInactive, PartInput NewPart);

public sealed record PartPage(PartDetail Detail, PartInput Edit);
