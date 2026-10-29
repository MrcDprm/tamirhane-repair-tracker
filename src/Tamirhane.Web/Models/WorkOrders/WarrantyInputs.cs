using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Web.Models.WorkOrders;

/// <summary>
/// Garanti kapsamında yeni kayıt formu. Müşteri ve cihaz önceki iş emrinden gelir; formda sadece bu geliş için
/// değişenler var (overposting yok). Garantinin sürüp sürmediğine domain karar verir.
/// </summary>
public sealed class WarrantyInput
{
    [Display(Name = "field.complaint")]
    [Required(ErrorMessage = "validation.complaint.required")]
    [StringLength(1000, ErrorMessage = "validation.max_chars")]
    public string Complaint { get; set; } = "";

    [Display(Name = "field.condition")]
    [StringLength(1000, ErrorMessage = "validation.max_chars")]
    public string? Condition { get; set; }

    [Display(Name = "field.accessories")]
    [StringLength(1000, ErrorMessage = "validation.max_chars")]
    public string? Accessories { get; set; }

    [Display(Name = "field.passcode")]
    [StringLength(100, ErrorMessage = "validation.max_chars")]
    public string? Passcode { get; set; }
}

/// <summary>Formun sayfası: önceki iş emrinin özeti ve girilenler.</summary>
public sealed record WarrantyPage(WorkOrderView Previous, WarrantyInput Input);
