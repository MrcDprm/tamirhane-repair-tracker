using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

public sealed record PaymentCommand(Guid WorkOrderId, decimal Amount, PaymentMethod Method, string? Note);

public sealed class RecordPayment(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<Guid>> HandleAsync(PaymentCommand c, CancellationToken ct) =>
        RunAsync(c.WorkOrderId, order => Task.FromResult(IdOf(order.RecordPayment(c.Amount, c.Method, User.StaffId, Now, c.Note))), ct);

    internal static Result<Guid> IdOf(Result<Payment> payment) =>
        payment.IsSuccess ? Result.Success(payment.Value.Id) : Result.Failure<Guid>(payment.Error!);
}

/// <summary>Para iadesi kasadan çıkış olduğu için sadece atölye sahibine açıktır.</summary>
public sealed class RecordRefund(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<Guid>> HandleAsync(PaymentCommand c, CancellationToken ct) =>
        User.IsOwner
            ? RunAsync(c.WorkOrderId, order => Task.FromResult(RecordPayment.IdOf(order.RecordRefund(c.Amount, c.Method, User.StaffId, Now, c.Note))), ct)
            : Task.FromResult(Result.Failure<Guid>(ApplicationErrors.OwnerOnly));
}

public sealed class SetDiagnosisFee(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, decimal fee, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.SetDiagnosisFee(fee), ct);
}
