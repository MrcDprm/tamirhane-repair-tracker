using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Customers;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Web.Models.WorkOrders;

/// <summary>
/// Cihaz kabul formu. Sadece bu alanlar bağlanır: atölye, şube, personel, durum, numara ve takip kodu
/// formdan değil oturumdan ve sunucudan gelir (overposting yok). Asıl kurallar domain'de.
/// </summary>
public sealed class IntakeInput
{
    [Display(Name = "Telefon")]
    [Required(ErrorMessage = "Müşterinin telefonunu yaz.")]
    [StringLength(30)]
    public string Phone { get; set; } = "";

    /// <summary>Telefonla bulunan müşteri; boşsa yeni müşteri açılır.</summary>
    public Guid? CustomerId { get; set; }

    [Display(Name = "Müşterinin adı soyadı")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    public string? CustomerName { get; set; }

    [Display(Name = "E-posta (isteğe bağlı)")]
    [StringLength(254, ErrorMessage = "E-posta en fazla 254 karakter olabilir.")]
    public string? CustomerEmail { get; set; }

    [Display(Name = "Aydınlatma metni müşteriye okundu, kişisel verilerinin işlenmesini onayladı")]
    public bool KvkkConsent { get; set; }

    /// <summary>Müşterinin kayıtlı cihazı; boşsa yeni cihaz.</summary>
    public Guid? DeviceId { get; set; }

    [Display(Name = "Tür")]
    public DeviceKind DeviceKind { get; set; } = DeviceKind.Phone;

    [Display(Name = "Marka")]
    [StringLength(60, ErrorMessage = "Marka en fazla 60 karakter olabilir.")]
    public string? Brand { get; set; }

    [Display(Name = "Model")]
    [StringLength(100, ErrorMessage = "Model en fazla 100 karakter olabilir.")]
    public string? Model { get; set; }

    [Display(Name = "Seri no (isteğe bağlı)")]
    [StringLength(60, ErrorMessage = "Seri no en fazla 60 karakter olabilir.")]
    public string? Serial { get; set; }

    [Display(Name = "IMEI (isteğe bağlı)")]
    [StringLength(30)]
    public string? Imei { get; set; }

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

/// <summary>
/// Kabul sayfası. Searched=false: sadece telefon adımı. Searched=true: Customer doluysa kayıtlı müşteri, boşsa yeni müşteri.
/// </summary>
public sealed record IntakePage(bool Searched, CustomerLookup? Customer, IntakeInput Input);

/// <summary>İş emri sayfasındaki işlem düğmeleri. Hangi alanın kullanılacağı işleme göre değişir.</summary>
public sealed class StepInput
{
    public Tamirhane.Application.WorkOrders.WorkOrderAction Step { get; set; }

    /// <summary>İptal nedeni.</summary>
    [StringLength(1000)]
    public string? Reason { get; set; }

    /// <summary>Teklifi onaylayan ya da reddeden kişi (telefonla bildiren müşteri).</summary>
    [StringLength(100)]
    public string? DecidedBy { get; set; }

    /// <summary>Borçlu teslim (sadece sahip; senaryo da denetler).</summary>
    public bool AllowBalanceDue { get; set; }
}

public sealed class NoteInput
{
    [StringLength(1000)]
    public string? Text { get; set; }

    public bool VisibleToCustomer { get; set; }
}

/// <summary>Teklif kalemi formu. Parça kalemi stoktan seçilir; serbest kalemde açıklama ve fiyat yazılır.</summary>
public sealed class QuoteLineInput
{
    public Tamirhane.Domain.WorkOrders.QuoteLineKind Kind { get; set; }

    public Guid? PartId { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    public decimal Quantity { get; set; } = 1;

    /// <summary>Boşsa stoktaki parçanın satış fiyatı.</summary>
    public decimal? UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    /// <summary>Boşsa atölyenin varsayılan KDV oranı.</summary>
    public decimal? VatRate { get; set; }
}

/// <summary>Tahsilat ya da iade formu.</summary>
public sealed class PaymentInput
{
    public decimal Amount { get; set; }

    public Tamirhane.Domain.WorkOrders.PaymentMethod Method { get; set; }

    [StringLength(200)]
    public string? Note { get; set; }
}
