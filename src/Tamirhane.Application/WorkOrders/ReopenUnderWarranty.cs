using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

public sealed record ReopenUnderWarrantyCommand(
    Guid PreviousWorkOrderId, string Complaint, string? Condition, string? Accessories, string? Passcode);

/// <summary>
/// Garanti kapsamında geri gelen cihaz için yeni iş emri. Müşteri ve cihaz önceki iş emrinden gelir;
/// önceki iş emri bu atölyenin olmalı, teslim edilmiş ve garantisi sürüyor olmalı.
/// </summary>
public sealed class ReopenUnderWarranty(
    IWorkOrderRepository orders, IUnitOfWork unitOfWork, INotificationQueue notifications,
    ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<ReceivedWorkOrder>> HandleAsync(ReopenUnderWarrantyCommand c, CancellationToken ct)
    {
        var previous = await orders.GetAsync(c.PreviousWorkOrderId, ct);
        if (previous is null)
            return ApplicationErrors.WorkOrderNotFound;

        var now = TurkeyTime.Now(clock);
        var number = WorkOrderNumber.Format(now.Year, await orders.NextSequenceAsync(user.ShopId, now.Year, ct));
        var order = WorkOrder.ReceiveUnderWarranty(previous, user.BranchId, number, TrackingCode.Generate(),
            c.Complaint, c.Condition, c.Accessories, c.Passcode, user.StaffId, now);
        if (order.IsFailure)
            return order.Error!;

        orders.Add(order.Value);
        await unitOfWork.SaveChangesAsync(ct);
        notifications.Enqueue(new WorkOrderNotification(user.ShopId, order.Value.Id, order.Value.Status));
        return new ReceivedWorkOrder(order.Value.Id, order.Value.Number, order.Value.TrackingCode);
    }
}
