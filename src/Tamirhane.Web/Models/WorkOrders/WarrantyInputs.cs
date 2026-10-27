using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Web.Models.WorkOrders;

/// <summary>
/// Garanti kapsamında yeni kayıt formu. Müşteri ve cihaz önceki iş emrinden gelir; formda sadece bu geliş için
/// değişenler var (overposting yok). Garantinin sürüp sürmediğine domain karar verir.
/// </summary>
public sealed class WarrantyInput
{
    [Display(Name = "Müşterinin şikâyeti")]
    [Required(ErrorMessage = "Müşterinin şikâyetini yaz.")]
    [StringLength(1000, ErrorMessage = "En fazla 1000 karakter.")]
    public string Complaint { get; set; } = "";

    [Display(Name = "Görünür durum (isteğe bağlı)")]
    [StringLength(1000, ErrorMessage = "En fazla 1000 karakter.")]
    public string? Condition { get; set; }

    [Display(Name = "Teslim alınan aksesuarlar (isteğe bağlı)")]
    [StringLength(1000, ErrorMessage = "En fazla 1000 karakter.")]
    public string? Accessories { get; set; }

    [Display(Name = "Ekran kilidi ya da cihaz şifresi (isteğe bağlı)")]
    [StringLength(100, ErrorMessage = "En fazla 100 karakter.")]
    public string? Passcode { get; set; }
}

/// <summary>Formun sayfası: önceki iş emrinin özeti ve girilenler.</summary>
public sealed record WarrantyPage(WorkOrderView Previous, WarrantyInput Input);
