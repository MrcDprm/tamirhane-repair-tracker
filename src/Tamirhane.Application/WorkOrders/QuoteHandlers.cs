using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

public sealed record AddQuoteLineCommand(
    Guid WorkOrderId, QuoteLineKind Kind, Guid? PartId, string? Description,
    decimal Quantity, decimal UnitPrice, decimal VatRate, decimal DiscountRate);

/// <summary>Teklife kalem ekler. Stoktan parça seçildiyse parça bu atölyenin olmalı ve kullanımda olmalı.</summary>
public sealed class AddQuoteLine(IWorkOrderRepository orders, IPartRepository parts, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<Guid>> HandleAsync(AddQuoteLineCommand command, CancellationToken ct) =>
        RunAsync<Guid>(command.WorkOrderId, async order =>
        {
            var description = command.Description;
            if (command.Kind == QuoteLineKind.Part && command.PartId is { } partId)
            {
                var part = await parts.GetAsync(partId, ct);
                if (part is null || !part.IsActive)
                    return ApplicationErrors.PartNotFound;
                if (string.IsNullOrWhiteSpace(description))
                    description = part.Name;
            }

            var line = order.AddQuoteLine(command.Kind, command.PartId, description ?? "",
                command.Quantity, command.UnitPrice, command.VatRate, command.DiscountRate);
            return line.IsSuccess ? Result.Success(line.Value.Id) : Result.Failure<Guid>(line.Error!);
        }, ct);
}

public sealed class RemoveQuoteLine(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, Guid lineId, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.RemoveQuoteLine(lineId), ct);
}

/// <summary>Müşteri kararını telefonla bildirdi; personel onun adına kaydeder.</summary>
public sealed class RecordQuoteDecision(IWorkOrderRepository orders, IUnitOfWork uow, INotificationQueue queue, ICurrentUser user, TimeProvider clock)
    : WorkOrderHandler(orders, uow, queue, user, clock)
{
    public Task<Result<bool>> HandleAsync(Guid workOrderId, bool approved, string customerName, CancellationToken ct) =>
        RunAsync(workOrderId, order => order.DecideQuote(approved, customerName, User.StaffId, Now), ct);
}

/// <summary>
/// Müşteri teklifi takip sayfasından onaylar ya da reddeder. Giriş yoktur: erişim takip koduyla,
/// bu yüzden ICurrentUser kullanılmaz ve kayıt personel olmadan yapılır.
/// </summary>
public sealed class DecideQuoteByCustomer(IWorkOrderRepository orders, IUnitOfWork unitOfWork, INotificationQueue notifications, TimeProvider clock)
{
    public async Task<Result> HandleAsync(string trackingCode, bool approved, string customerName, CancellationToken ct)
    {
        var code = TrackingCode.Normalize(trackingCode);
        var order = code is null ? null : await orders.GetByTrackingCodeAsync(code, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var result = order.DecideQuote(approved, customerName, null, TurkeyTime.Now(clock));
        if (result.IsFailure)
            return result;

        await unitOfWork.SaveChangesAsync(ct);
        notifications.Enqueue(new WorkOrderNotification(order.ShopId, order.Id, order.Status));
        return Result.Success();
    }
}
