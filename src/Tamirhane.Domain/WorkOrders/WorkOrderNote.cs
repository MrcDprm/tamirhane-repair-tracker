using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// İş emrine düşülen not. Teknisyen notu sadece atölyede görünür; müşteriye görünen not
/// takip sayfasında da çıkar (ör. "Parça siparişi verildi, Perşembe gelecek").
/// </summary>
public sealed class WorkOrderNote : Entity
{
    private WorkOrderNote() { }

    internal WorkOrderNote(Guid workOrderId, string text, bool visibleToCustomer, Guid staffId, DateTimeOffset at)
    {
        WorkOrderId = workOrderId;
        Text = text;
        VisibleToCustomer = visibleToCustomer;
        StaffId = staffId;
        At = at;
    }

    public Guid WorkOrderId { get; private set; }
    public string Text { get; private set; } = "";
    public bool VisibleToCustomer { get; private set; }
    public Guid StaffId { get; private set; }
    public DateTimeOffset At { get; private set; }
}
