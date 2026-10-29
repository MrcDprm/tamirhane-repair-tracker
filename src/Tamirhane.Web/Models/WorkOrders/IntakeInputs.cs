using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Customers;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Web.Models.WorkOrders;

/// <summary>
/// Cihaz kabul formu. Sadece bu alanlar bağlanır: atölye, şube, personel, durum, numara ve takip kodu
/// formdan değil oturumdan ve sunucudan gelir (overposting yok). Asıl kurallar domain'de; alan adları ve mesajlar Resources/*.json anahtarları (#35).
/// </summary>
public sealed class IntakeInput
{
    [Display(Name = "field.phone")]
    [Required(ErrorMessage = "validation.phone.required")]
    [StringLength(30, ErrorMessage = "validation.max_chars")]
    public string Phone { get; set; } = "";

    /// <summary>Telefonla bulunan müşteri; boşsa yeni müşteri açılır.</summary>
    public Guid? CustomerId { get; set; }

    [Display(Name = "field.customer_name")]
    [StringLength(100, ErrorMessage = "validation.max_chars")]
    public string? CustomerName { get; set; }

    [Display(Name = "field.email_optional")]
    [StringLength(254, ErrorMessage = "validation.max_chars")]
    public string? CustomerEmail { get; set; }

    [Display(Name = "field.kvkk_consent")]
    public bool KvkkConsent { get; set; }

    /// <summary>Müşterinin kayıtlı cihazı; boşsa yeni cihaz.</summary>
    public Guid? DeviceId { get; set; }

    [Display(Name = "field.device_kind")]
    public DeviceKind DeviceKind { get; set; } = DeviceKind.Phone;

    [Display(Name = "field.brand")]
    [StringLength(60, ErrorMessage = "validation.max_chars")]
    public string? Brand { get; set; }

    [Display(Name = "field.model")]
    [StringLength(100, ErrorMessage = "validation.max_chars")]
    public string? Model { get; set; }

    [Display(Name = "field.serial")]
    [StringLength(60, ErrorMessage = "validation.max_chars")]
    public string? Serial { get; set; }

    [Display(Name = "field.imei")]
    [StringLength(30, ErrorMessage = "validation.max_chars")]
    public string? Imei { get; set; }

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

    [Display(Name = "field.promised_date")]
    public DateOnly? PromisedDate { get; set; }
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

/// <summary>Söz verilen teslim günü; boş gönderilirse söz kaldırılır.</summary>
public sealed class PromiseInput
{
    public DateOnly? Date { get; set; }
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
