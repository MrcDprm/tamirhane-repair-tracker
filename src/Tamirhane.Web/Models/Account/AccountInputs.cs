using System.ComponentModel.DataAnnotations;

namespace Tamirhane.Web.Models.Account;

// Her form kendi giriş modeliyle bağlanır: formda olmayan bir alan (atölye, rol) istekle gönderilse de modele girmez.
// Uzunluk sınırları kabaca; asıl kurallar domain'de ve Identity'de, hata metinleri AccountController'da.
// Alan adları ve mesajlar Resources/*.json anahtarlarıdır (#35).

public sealed class RegisterInput
{
    [Display(Name = "field.shop_name")]
    [Required(ErrorMessage = "validation.shop_name.required")]
    [StringLength(100, ErrorMessage = "validation.shop_name.length")]
    public string ShopName { get; set; } = "";

    [Display(Name = "field.your_name")]
    [Required(ErrorMessage = "validation.your_name.required")]
    [StringLength(100, ErrorMessage = "validation.name.length")]
    public string OwnerName { get; set; } = "";

    [Display(Name = "field.email")]
    [Required(ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256, ErrorMessage = "validation.email.length")]
    public string Email { get; set; } = "";

    [Display(Name = "field.password")]
    [Required(ErrorMessage = "validation.password.choose")]
    [StringLength(128, ErrorMessage = "validation.password.length")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>Müşteriye giden e-postaların ve takip sayfasının dili.</summary>
    [Display(Name = "field.customer_language")]
    [Required(ErrorMessage = "validation.language.required")]
    public string Locale { get; set; } = "tr";
}

public sealed class LoginInput
{
    [Display(Name = "field.email")]
    [Required(ErrorMessage = "validation.email.required")]
    [StringLength(256)]
    public string Email { get; set; } = "";

    [Display(Name = "field.password")]
    [Required(ErrorMessage = "validation.password.required")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "field.remember_me")]
    public bool RememberMe { get; set; }

    /// <summary>Girişten sonra dönülecek sayfa; sadece sitenin kendi adresiyse kullanılır.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Yeni şifre formu. Hesap ve token e-postadaki linkten gizli alanlara gelir; doğruluğuna Identity bakar.</summary>
public sealed class ResetPasswordInput
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = "";

    [Display(Name = "field.new_password")]
    [Required(ErrorMessage = "validation.new_password.required")]
    [StringLength(128, ErrorMessage = "validation.password.length")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "field.new_password_again")]
    [Required(ErrorMessage = "validation.new_password.again")]
    [Compare(nameof(Password), ErrorMessage = "validation.password.mismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class EmailInput
{
    [Display(Name = "field.email")]
    [Required(ErrorMessage = "validation.email.required")]
    [EmailAddress(ErrorMessage = "validation.email.invalid")]
    [StringLength(256)]
    public string Email { get; set; } = "";
}
