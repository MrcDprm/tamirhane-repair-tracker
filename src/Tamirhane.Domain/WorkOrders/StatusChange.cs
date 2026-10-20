using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// İş emrinin geçmişindeki tek bir adım. Takip sayfasındaki her kaşe bir StatusChange'tir.
/// From, ilk kayıtta (cihaz kabulü) boştur. StaffId, müşterinin kendi kararında boştur.
/// </summary>
public sealed class StatusChange : Entity
{
    private StatusChange() { }

    internal StatusChange(Guid workOrderId, WorkOrderStatus? from, WorkOrderStatus to, Guid? staffId, DateTimeOffset at, string? note)
    {
        WorkOrderId = workOrderId;
        From = from;
        To = to;
        StaffId = staffId;
        At = at;
        Note = note;
    }

    public Guid WorkOrderId { get; private set; }
    public WorkOrderStatus? From { get; private set; }
    public WorkOrderStatus To { get; private set; }
    public Guid? StaffId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Note { get; private set; }
}
