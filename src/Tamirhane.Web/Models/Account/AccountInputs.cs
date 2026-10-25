using System.ComponentModel.DataAnnotations;

namespace Tamirhane.Web.Models.Account;

// Her form kendi giriş modeliyle bağlanır: formda olmayan bir alan (atölye, rol) istekle gönderilse de modele girmez.
// Uzunluk sınırları kabaca; asıl kurallar domain'de ve Identity'de, hata metinleri AccountController'da.

public sealed class RegisterInput
{
    [Display(Name = "Atölyenin adı")]
    [Required(ErrorMessage = "Atölyenin adını yazın.")]
    [StringLength(100, ErrorMessage = "Atölye adı en fazla 100 karakter olabilir.")]
    public string ShopName { get; set; } = "";

    [Display(Name = "Adınız")]
    [Required(ErrorMessage = "Adınızı yazın.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    public string OwnerName { get; set; } = "";

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi yazın.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")]
    [StringLength(256, ErrorMessage = "E-posta en fazla 256 karakter olabilir.")]
    public string Email { get; set; } = "";

    [Display(Name = "Şifre")]
    [Required(ErrorMessage = "Bir şifre belirleyin.")]
    [StringLength(128, ErrorMessage = "Şifre en fazla 128 karakter olabilir.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>Müşteriye giden e-postaların ve takip sayfasının dili.</summary>
    [Display(Name = "Müşterilere giden yazıların dili")]
    [Required(ErrorMessage = "Bir dil seçin.")]
    public string Locale { get; set; } = "tr";
}

public sealed class LoginInput
{
    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi yazın.")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Display(Name = "Şifre")]
    [Required(ErrorMessage = "Şifrenizi yazın.")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Bu cihazda oturumum açık kalsın")]
    public bool RememberMe { get; set; }

    /// <summary>Girişten sonra dönülecek sayfa; sadece sitenin kendi adresiyse kullanılır.</summary>
    public string? ReturnUrl { get; set; }
}

public sealed class EmailInput
{
    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "E-posta adresinizi yazın.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")]
    [StringLength(256)]
    public string Email { get; set; } = "";
}
