using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Application.WorkOrders;

/// <summary>
/// Kabul fişi ve cihaz etiketi. Cihaz şifresi fişe basılmaz, sadece alınıp alınmadığı.
/// Etiket cihazın üstünde durur ve atölyede başkaları da görür: orada müşterinin sadece adı ve soyadının baş harfi var.
/// </summary>
public sealed record ReceiptView(
    string Number, string TrackingCode, Uri TrackingUrl, DateTimeOffset CreatedAt,
    string ShopName, string BranchName, string? BranchAddress, int WarrantyDays, string? ReceivedBy,
    string CustomerName, string CustomerPhone, string? CustomerEmail,
    DeviceKind DeviceKind, string Brand, string Model, string? Serial, string? Imei,
    string Complaint, string? Condition, string? Accessories, bool HasPasscode)
{
    /// <summary>"Zeynep Arslan" → "Zeynep A." (cihaz etiketi için).</summary>
    public string CustomerShortName
    {
        get
        {
            var parts = CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 2 ? CustomerName : $"{string.Join(' ', parts[..^1])} {parts[^1][0]}.";
        }
    }
}

public sealed class GetReceipt(
    IWorkOrderRepository orders, ICustomerRepository customers, IDeviceRepository devices, IShopRepository shops,
    IBranchRepository branches, IStaffRepository staff, IShopSettings settings, ITrackingLinks links, ICurrentUser user)
{
    public async Task<Result<ReceiptView>> HandleAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.GetAsync(id, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var customer = await customers.GetAsync(order.CustomerId, ct) ?? throw new InvalidOperationException("İş emrinin müşterisi yok.");
        var device = await devices.GetAsync(order.DeviceId, ct) ?? throw new InvalidOperationException("İş emrinin cihazı yok.");
        var shop = await shops.GetAsync(user.ShopId, ct) ?? throw new InvalidOperationException("Oturumun atölyesi yok.");
        var branch = await branches.GetAsync(order.BranchId, ct);
        var receivedBy = order.History.OrderBy(h => h.At).FirstOrDefault()?.StaffId is { } s
            ? (await staff.NamesAsync([s], ct)).GetValueOrDefault(s)
            : null;
        var warranty = (await settings.GetAsync(user.ShopId, ct)).WarrantyDays;

        return new ReceiptView(
            order.Number, order.TrackingCode, links.For(order.TrackingCode), order.CreatedAt,
            shop.Name, branch?.Name ?? "", branch?.Address, warranty, receivedBy,
            customer.Name, customer.Phone.Value, customer.Email,
            device.Kind, device.Brand, device.Model, device.Serial, device.Imei,
            order.Complaint, order.Condition, order.Accessories, order.Passcode is not null);
    }
}
