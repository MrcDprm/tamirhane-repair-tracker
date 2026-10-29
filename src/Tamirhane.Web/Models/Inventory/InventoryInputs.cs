using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Inventory;

namespace Tamirhane.Web.Models.Inventory;

// Uzunluk ve aralık sınırları kabaca; asıl kurallar domain'de (Part, StockLevel), hata metinleri InventoryController'da.
// Alan adları ve mesajlar Resources/*.json anahtarlarıdır (#35).

public sealed class PartInput
{
    [Display(Name = "field.sku")]
    [Required(ErrorMessage = "validation.sku.required")]
    [StringLength(40, ErrorMessage = "validation.max_chars")]
    public string Sku { get; set; } = "";

    [Display(Name = "field.part_name")]
    [Required(ErrorMessage = "validation.part_name.required")]
    [StringLength(200, ErrorMessage = "validation.max_chars")]
    public string Name { get; set; } = "";

    [Display(Name = "field.cost_price")]
    public decimal CostPrice { get; set; }

    [Display(Name = "field.sale_price")]
    public decimal SalePrice { get; set; }

    [Display(Name = "field.critical_level")]
    [Range(0, 1_000_000, ErrorMessage = "error.critical_level")]
    public int CriticalLevel { get; set; }

    /// <summary>Sadece yeni parçada: elde olan adet, "alış" olarak girer.</summary>
    [Display(Name = "field.opening_quantity")]
    [Range(0, 1_000_000, ErrorMessage = "validation.quantity.range")]
    public int OpeningQuantity { get; set; }
}

public sealed record InventoryPage(PartList List, string? Query, bool LowOnly, bool IncludeInactive, PartInput NewPart);

public sealed record PartPage(PartDetail Detail, PartInput Edit);
