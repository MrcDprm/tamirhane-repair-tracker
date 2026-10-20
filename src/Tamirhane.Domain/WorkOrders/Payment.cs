using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,
}

public enum PaymentKind
{
    Payment,
    Refund,
}

/// <summary>
/// Alınan ya da iade edilen para. Para sistemin içinden geçmez; bu sadece kasanın kaydıdır.
/// Tutar her zaman pozitiftir; yönü <see cref="Kind"/> belirler.
/// </summary>
public sealed class Payment : Entity
{
    public const int MaxNoteLength = 200;

    private Payment() { }

    internal Payment(Guid workOrderId, PaymentKind kind, decimal amount, PaymentMethod method,
        Guid staffId, DateTimeOffset at, string? note)
    {
        WorkOrderId = workOrderId;
        Kind = kind;
        Amount = amount;
        Method = method;
        StaffId = staffId;
        At = at;
        Note = note;
    }

    public Guid WorkOrderId { get; private set; }
    public PaymentKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public Guid StaffId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Kasaya etkisi: ödeme artı, iade eksi.</summary>
    public decimal SignedAmount => Kind == PaymentKind.Payment ? Amount : -Amount;
}
