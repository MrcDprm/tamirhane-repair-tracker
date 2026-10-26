using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>
/// Atölyedeki iş emri işlemlerinin ortak akışı: yükle → domain metodunu çağır → başarılıysa kaydet →
/// durum değiştiyse müşteriye bildirim kuyruğa. Başarısız işlemde hiçbir şey kaydedilmez.
/// </summary>
public abstract class WorkOrderHandler(
    IWorkOrderRepository orders, IUnitOfWork unitOfWork, INotificationQueue notifications,
    ICurrentUser user, TimeProvider clock)
{
    protected ICurrentUser User => user;
    protected DateTimeOffset Now => TurkeyTime.Now(clock);

    protected async Task<Result<T>> RunAsync<T>(Guid workOrderId, Func<WorkOrder, Task<Result<T>>> action, CancellationToken ct)
    {
        var order = await orders.GetAsync(workOrderId, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var before = order.Status;
        var result = await action(order);
        if (result.IsFailure)
            return result;

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return ApplicationErrors.ConcurrentChange;
        }
        if (order.Status != before)
            notifications.Enqueue(new WorkOrderNotification(order.ShopId, order.Id, order.Status));
        return result;
    }

    protected Task<Result<bool>> RunAsync(Guid workOrderId, Func<WorkOrder, Result> action, CancellationToken ct) =>
        RunAsync(workOrderId, order =>
        {
            var result = action(order);
            return Task.FromResult(result.IsSuccess ? Result.Success(true) : Result.Failure<bool>(result.Error!));
        }, ct);
}
