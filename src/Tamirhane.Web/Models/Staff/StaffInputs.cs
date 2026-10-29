using System.ComponentModel.DataAnnotations;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.Models.Staff;

// Alan adları ve mesajlar Resources/*.json anahtarlarıdır (#35).

public sealed class InviteInput
{
    [Display(Name = "field.technician_email")]
    [Required(ErrorMessage = "validation.invite_email.required")]
    [EmailAddress(ErrorMessage = "error.email")]
    [StringLength(256, ErrorMessage = "validation.max_chars")]
    public string Email { get; set; } = "";
}

public sealed record StaffPage(StaffOverview Overview, InviteInput Invite);

/// <summary>Davet kabul formu. Token e-postadaki linkten gizli alana gelir; atölye ve rol formdan alınmaz, davetten gelir.</summary>
public sealed class AcceptInvitationInput
{
    public string Token { get; set; } = "";

    [Display(Name = "field.full_name")]
    [Required(ErrorMessage = "validation.full_name.required")]
    [StringLength(100, ErrorMessage = "validation.max_chars")]
    public string DisplayName { get; set; } = "";

    [Display(Name = "field.password")]
    [Required(ErrorMessage = "validation.password.set")]
    [StringLength(128, ErrorMessage = "validation.password.length")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "field.password_again")]
    [Required(ErrorMessage = "validation.password.again")]
    [Compare(nameof(Password), ErrorMessage = "validation.password.mismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed record AcceptInvitationPage(InvitationView Invitation, AcceptInvitationInput Input);
