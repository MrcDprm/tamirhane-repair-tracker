using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

public static class PaymentErrors
{
    public static readonly Error InvalidAmount = Error.Validation("payment.invalid_amount");
    public static readonly Error Overpayment = Error.Validation("payment.overpayment");
    public static readonly Error RefundTooLarge = Error.Validation("payment.refund_too_large");
    public static readonly Error NoteTooLong = Error.Validation("payment.note_too_long");
    public static readonly Error NotAllowed = Error.Conflict("payment.not_allowed");
    public static readonly Error BalanceDue = Error.Conflict("payment.balance_due");
    public static readonly Error RefundDue = Error.Conflict("payment.refund_due");
    public static readonly Error RefundRequired = Error.Conflict("payment.refund_required");
    public static readonly Error InvalidDiagnosisFee = Error.Validation("payment.invalid_diagnosis_fee");
}
