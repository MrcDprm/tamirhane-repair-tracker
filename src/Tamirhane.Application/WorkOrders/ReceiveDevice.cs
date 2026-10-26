using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>
/// Cihaz kabul formu. Müşteri ve cihaz ya mevcut kayıtlardan seçilir (Id) ya da aynı formda yeni girilir.
/// </summary>
public sealed record ReceiveDeviceCommand(
    Guid? CustomerId, string? CustomerName, string? CustomerPhone, string? CustomerEmail, bool KvkkConsent,
    Guid? DeviceId, DeviceKind DeviceKind, string? Brand, string? Model, string? Serial, string? Imei,
    string Complaint, string? Condition, string? Accessories, string? Passcode, DateOnly? PromisedDate = null);

public sealed record ReceivedWorkOrder(Guid WorkOrderId, string Number, string TrackingCode);

public sealed class ReceiveDevice(
    ICustomerRepository customers, IDeviceRepository devices, IWorkOrderRepository orders,
    IUnitOfWork unitOfWork, INotificationQueue notifications, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<ReceivedWorkOrder>> HandleAsync(ReceiveDeviceCommand c, CancellationToken ct)
    {
        var now = TurkeyTime.Now(clock);

        var customer = await ResolveCustomerAsync(c, now, ct);
        if (customer.IsFailure)
            return customer.Error!;

        var device = await ResolveDeviceAsync(c, customer.Value, ct);
        if (device.IsFailure)
            return device.Error!;

        var number = WorkOrderNumber.Format(now.Year, await orders.NextSequenceAsync(user.ShopId, now.Year, ct));
        var order = WorkOrder.Receive(user.ShopId, user.BranchId, number, TrackingCode.Generate(),
            customer.Value.Id, device.Value.Id, c.Complaint, c.Condition, c.Accessories, c.Passcode, user.StaffId, now);
        if (order.IsFailure)
            return order.Error!;
        var promised = order.Value.Promise(c.PromisedDate, DateOnly.FromDateTime(now.DateTime));
        if (promised.IsFailure)
            return promised.Error!;

        orders.Add(order.Value);
        await unitOfWork.SaveChangesAsync(ct);
        notifications.Enqueue(new WorkOrderNotification(user.ShopId, order.Value.Id, order.Value.Status));
        return new ReceivedWorkOrder(order.Value.Id, order.Value.Number, order.Value.TrackingCode);
    }

    private async Task<Result<Customer>> ResolveCustomerAsync(ReceiveDeviceCommand c, DateTimeOffset now, CancellationToken ct)
    {
        if (c.CustomerId is { } id)
            return await customers.GetAsync(id, ct) is { } existing ? existing : ApplicationErrors.CustomerNotFound;

        // Aynı telefonla ikinci müşteri açılmaz; form önce telefonla arayıp mevcut müşteriyi seçtirir.
        var phone = PhoneNumber.Parse(c.CustomerPhone);
        if (phone.IsFailure)
            return phone.Error!;
        if (await customers.FindByPhoneAsync(phone.Value, ct) is not null)
            return ApplicationErrors.DuplicatePhone;

        var created = Customer.Create(user.ShopId, c.CustomerName ?? "", phone.Value.Value, c.CustomerEmail, c.KvkkConsent, now);
        if (created.IsSuccess)
            customers.Add(created.Value);
        return created;
    }

    private async Task<Result<Device>> ResolveDeviceAsync(ReceiveDeviceCommand c, Customer owner, CancellationToken ct)
    {
        if (c.DeviceId is { } id)
        {
            var existing = await devices.GetAsync(id, ct);
            // Başka müşterinin cihazı seçilemez.
            return existing is not null && existing.CustomerId == owner.Id ? existing : ApplicationErrors.DeviceNotFound;
        }

        var created = Device.Create(owner, c.DeviceKind, c.Brand ?? "", c.Model ?? "", c.Serial, c.Imei);
        if (created.IsSuccess)
            devices.Add(created.Value);
        return created;
    }
}
