using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Shops;

public static class ShopErrors
{
    public static readonly Error InvalidName = Error.Validation("shop.invalid_name");
    public static readonly Error InvalidLocale = Error.Validation("shop.invalid_locale");
    public static readonly Error InvalidWarrantyDays = Error.Validation("shop.invalid_warranty_days");
    public static readonly Error InvalidVatRate = Error.Validation("shop.invalid_vat_rate");
    public static readonly Error InvalidBranchName = Error.Validation("branch.invalid_name");
    public static readonly Error InvalidBranchAddress = Error.Validation("branch.invalid_address");
    public static readonly Error BranchNotFound = Error.NotFound("branch.not_found");
    public static readonly Error TooManyBranches = Error.Conflict("branch.too_many");
    public static readonly Error InvalidDisplayName = Error.Validation("staff.invalid_display_name");
    public static readonly Error InvalidRole = Error.Validation("staff.invalid_role");
    public static readonly Error InvalidEmail = Error.Validation("staff.invalid_email");
    public static readonly Error CannotRemoveOwner = Error.Validation("staff.cannot_remove_owner");
    public static readonly Error StaffNotFound = Error.NotFound("staff.not_found");
    public static readonly Error InvalidInvitation = Error.Validation("invitation.invalid");
}
