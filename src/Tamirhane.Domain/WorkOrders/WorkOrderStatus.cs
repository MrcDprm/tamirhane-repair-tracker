namespace Tamirhane.Domain.WorkOrders;

public enum WorkOrderStatus
{
    Received,
    Diagnosing,
    AwaitingApproval,
    InRepair,
    Ready,
    Delivered,
    Declined,
    Cancelled,
}
