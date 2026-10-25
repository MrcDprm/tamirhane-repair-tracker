using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Shops;

public static class ShopErrors
{
    public static readonly Error InvalidName = Error.Validation("shop.invalid_name");
    public static readonly Error InvalidLocale = Error.Validation("shop.invalid_locale");
    public static readonly Error InvalidDisplayName = Error.Validation("staff.invalid_display_name");
    public static readonly Error InvalidRole = Error.Validation("staff.invalid_role");
}
