using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

public static class WorkOrderErrors
{
    public static readonly Error InvalidTransition = Error.Conflict("work_order.invalid_transition");
    public static readonly Error ComplaintRequired = Error.Validation("work_order.complaint_required");
    public static readonly Error TextTooLong = Error.Validation("work_order.text_too_long");
    public static readonly Error InvalidWarrantyDays = Error.Validation("work_order.invalid_warranty_days");
    public static readonly Error NotUnderWarranty = Error.Conflict("work_order.not_under_warranty");
    public static readonly Error CancelReasonRequired = Error.Validation("work_order.cancel_reason_required");
}
