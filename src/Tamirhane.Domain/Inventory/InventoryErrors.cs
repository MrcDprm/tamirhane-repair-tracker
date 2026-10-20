using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Inventory;

public static class InventoryErrors
{
    public static readonly Error InvalidSku = Error.Validation("inventory.invalid_sku");
    public static readonly Error InvalidName = Error.Validation("inventory.invalid_name");
    public static readonly Error InvalidPrice = Error.Validation("inventory.invalid_price");
    public static readonly Error InvalidCriticalLevel = Error.Validation("inventory.invalid_critical_level");
    public static readonly Error InvalidQuantity = Error.Validation("inventory.invalid_quantity");
    public static readonly Error ReasonRequired = Error.Validation("inventory.reason_required");
    public static readonly Error NoteTooLong = Error.Validation("inventory.note_too_long");
    public static readonly Error InsufficientStock = Error.Conflict("inventory.insufficient_stock");
    public static readonly Error WrongBranch = Error.Conflict("inventory.wrong_branch");
}
