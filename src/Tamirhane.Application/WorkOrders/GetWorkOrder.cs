using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>
/// İş emri sayfasının bilgileri (atölye içi; müşterinin tam adı ve telefonu görünür).
/// Cihaz şifresi sayfaya gönderilmez, sadece kayıtlı olup olmadığı.
/// </summary>
public sealed record WorkOrderView(
    Guid Id, string Number, string TrackingCode, WorkOrderStatus Status, DateTimeOffset CreatedAt,
    string Complaint, string? Condition, string? Accessories, bool HasPasscode,
    string CustomerName, string CustomerPhone, string? CustomerEmail,
    DeviceKind DeviceKind, string Brand, string Model, string? Serial, string? Imei);

public sealed class GetWorkOrder(IWorkOrderRepository orders, ICustomerRepository customers, IDeviceRepository devices)
{
    public async Task<Result<WorkOrderView>> HandleAsync(Guid id, CancellationToken ct)
    {
        // Başka atölyenin iş emri kiracı filtresinden dolayı bulunmaz: "yok" döner, varlığı sızmaz.
        var order = await orders.GetAsync(id, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var customer = await customers.GetAsync(order.CustomerId, ct) ?? throw new InvalidOperationException("İş emrinin müşterisi yok.");
        var device = await devices.GetAsync(order.DeviceId, ct) ?? throw new InvalidOperationException("İş emrinin cihazı yok.");
        return new WorkOrderView(
            order.Id, order.Number, order.TrackingCode, order.Status, order.CreatedAt,
            order.Complaint, order.Condition, order.Accessories, order.Passcode is not null,
            customer.Name, customer.Phone.Value, customer.Email,
            device.Kind, device.Brand, device.Model, device.Serial, device.Imei);
    }
}
