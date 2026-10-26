using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.WorkOrders;

public sealed record UpdateIntakeCommand(Guid WorkOrderId, string Complaint, string? Condition, string? Accessories, string? Passcode);

public sealed class UpdateIntakeDetails(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(UpdateIntakeCommand c, CancellationToken ct) =>
        RunAsync(c.WorkOrderId, order => order.UpdateIntake(c.Complaint, c.Condition, c.Accessories, c.Passcode), ct);
}

/// <summary>Teslim gününü söz verir, değiştirir ya da kaldırır (takip sayfasında "tahmini teslim").</summary>
public sealed class PromiseDelivery(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, DateOnly? date, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.Promise(date, DateOnly.FromDateTime(Now.DateTime)), ct);
}

public sealed class AddNote(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<Guid>> HandleAsync(Guid workOrderId, string text, bool visibleToCustomer, CancellationToken ct) =>
        RunAsync(workOrderId, order =>
        {
            var note = order.AddNote(text, visibleToCustomer, User.StaffId, Now);
            return Task.FromResult(note.IsSuccess ? Result.Success(note.Value.Id) : Result.Failure<Guid>(note.Error!));
        }, ct);
}
