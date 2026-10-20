using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.WorkOrders;

// İş emrinin durumunu ilerleten atölye işlemleri. Her biri tek bir kullanım senaryosu.

public sealed class StartDiagnosis(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.StartDiagnosis(User.StaffId, Now), ct);
}

/// <summary>Teklif gerekmeyen onarım (ücretsiz ya da garanti işi).</summary>
public sealed class StartRepair(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.StartRepair(User.StaffId, Now), ct);
}

public sealed class SendQuote(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.SendQuote(User.StaffId, Now), ct);
}

/// <summary>Onarım bitti: teklifteki stok parçaları şubenin stoğundan düşer, hareketler kaydedilir.</summary>
public sealed class MarkReady(IWorkOrderRepository orders, IStockRepository stock, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, CancellationToken ct) =>
        RunAsync(workOrderId, async order =>
        {
            var branchStock = await stock.GetBranchStockAsync(order.BranchId, PartIds(order), ct);
            var movements = order.MarkReady(User.StaffId, Now, branchStock);
            if (movements.IsFailure)
                return Result.Failure<bool>(movements.Error!);

            stock.AddMovements(movements.Value);
            return Result.Success(true);
        }, ct);

    internal static IEnumerable<Guid> PartIds(Domain.WorkOrders.WorkOrder order) =>
        order.QuoteLines.Where(l => l.PartId is not null).Select(l => l.PartId!.Value).Distinct();
}

/// <summary>İptal; iş emri hazırsa kullanılan parçalar stoğa geri döner.</summary>
public sealed class CancelWorkOrder(IWorkOrderRepository orders, IStockRepository stock, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, string reason, CancellationToken ct) =>
        RunAsync(workOrderId, async order =>
        {
            var branchStock = await stock.GetBranchStockAsync(order.BranchId, MarkReady.PartIds(order), ct);
            var movements = order.Cancel(User.StaffId, Now, reason, branchStock);
            if (movements.IsFailure)
                return Result.Failure<bool>(movements.Error!);

            stock.AddMovements(movements.Value);
            return Result.Success(true);
        }, ct);
}

/// <summary>Teslim. Borçlu teslimi sadece atölye sahibi onaylayabilir; garanti süresi atölye ayarından.</summary>
public sealed class DeliverWorkOrder(IWorkOrderRepository orders, IShopSettings settings, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public async Task<Result<bool>> HandleAsync(Guid workOrderId, bool allowBalanceDue, CancellationToken ct)
    {
        if (allowBalanceDue && !User.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var shop = await settings.GetAsync(User.ShopId, ct);
        return await RunAsync(workOrderId, order => order.Deliver(User.StaffId, Now, shop.WarrantyDays, allowBalanceDue), ct);
    }
}
