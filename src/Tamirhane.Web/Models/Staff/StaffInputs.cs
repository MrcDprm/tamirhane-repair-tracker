using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.Models.Staff;

public sealed class InviteInput
{
    [Display(Name = "Teknisyenin e-postası")]
    [Required(ErrorMessage = "Davet edeceğin kişinin e-posta adresini yaz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yaz.")]
    [StringLength(256, ErrorMessage = "E-posta en fazla 256 karakter olabilir.")]
    public string Email { get; set; } = "";
}

public sealed record StaffPage(StaffOverview Overview, InviteInput Invite);

/// <summary>Davet kabul formu. Token e-postadaki linkten gizli alana gelir; atölye ve rol formdan alınmaz, davetten gelir.</summary>
public sealed class AcceptInvitationInput
{
    public string Token { get; set; } = "";

    [Display(Name = "Adın soyadın")]
    [Required(ErrorMessage = "Adını yaz; iş emirlerinde bu ad görünür.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    public string DisplayName { get; set; } = "";

    [Display(Name = "Şifre")]
    [Required(ErrorMessage = "Bir şifre belirle.")]
    [StringLength(128, ErrorMessage = "Şifre en fazla 128 karakter olabilir.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Şifre (tekrar)")]
    [Required(ErrorMessage = "Şifreni bir kez daha yaz.")]
    [Compare(nameof(Password), ErrorMessage = "İki şifre aynı değil.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed record AcceptInvitationPage(InvitationView Invitation, AcceptInvitationInput Input);
