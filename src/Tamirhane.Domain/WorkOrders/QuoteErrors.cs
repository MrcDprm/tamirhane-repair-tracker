using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

public static class QuoteErrors
{
    public static readonly Error NotEditable = Error.Conflict("quote.not_editable");
    public static readonly Error Empty = Error.Validation("quote.empty");
    public static readonly Error LineNotFound = Error.NotFound("quote.line_not_found");
    public static readonly Error InvalidDescription = Error.Validation("quote.invalid_description");
    public static readonly Error InvalidQuantity = Error.Validation("quote.invalid_quantity");
    public static readonly Error PartQuantityMustBeWhole = Error.Validation("quote.part_quantity_must_be_whole");
    public static readonly Error InvalidUnitPrice = Error.Validation("quote.invalid_unit_price");
    public static readonly Error InvalidVatRate = Error.Validation("quote.invalid_vat_rate");
    public static readonly Error InvalidDiscount = Error.Validation("quote.invalid_discount");
    public static readonly Error DeciderNameRequired = Error.Validation("quote.decider_name_required");
}
