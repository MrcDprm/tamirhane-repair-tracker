using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Application.Customers;

public sealed record DeviceSummary(Guid Id, DeviceKind Kind, string Brand, string Model, string? Serial, string? Imei);

public sealed record CustomerLookup(Guid Id, string Name, string Phone, string? Email, IReadOnlyList<DeviceSummary> Devices);

/// <summary>
/// Cihaz kabulde ilk adım: telefonla müşteri arama. Bulunursa cihazlarıyla döner; bulunamazsa
/// başarılı ama boş sonuç (yeni müşteri formu açılır). Geçersiz telefon ise hata.
/// </summary>
public sealed class FindCustomerByPhone(ICustomerRepository customers, IDeviceRepository devices)
{
    public async Task<Result<CustomerLookup?>> HandleAsync(string phone, CancellationToken ct)
    {
        var parsed = PhoneNumber.Parse(phone);
        if (parsed.IsFailure)
            return parsed.Error!;

        var customer = await customers.FindByPhoneAsync(parsed.Value, ct);
        if (customer is null)
            return Result.Success<CustomerLookup?>(null);

        var owned = await devices.ListByCustomerAsync(customer.Id, ct);
        return new CustomerLookup(customer.Id, customer.Name, customer.Phone.Value, customer.Email,
            owned.Select(d => new DeviceSummary(d.Id, d.Kind, d.Brand, d.Model, d.Serial, d.Imei)).ToList());
    }
}
