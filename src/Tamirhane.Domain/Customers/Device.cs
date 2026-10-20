using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Customers;

public enum DeviceKind
{
    Phone,
    Tablet,
    Computer,
    Console,
    Watch,
    Appliance,
    Other,
}

/// <summary>Müşterinin cihazı. Aynı cihaz tekrar geldiğinde (ör. garanti) yeni kayıt açılmaz, bu kullanılır.</summary>
public sealed class Device : Entity
{
    public const int MaxBrandLength = 60;
    public const int MaxModelLength = 100;
    public const int MaxSerialLength = 60;

    private Device() { }

    public Guid ShopId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DeviceKind Kind { get; private set; }
    public string Brand { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string? Serial { get; private set; }
    public string? Imei { get; private set; }

    public static Result<Device> Create(Customer owner, DeviceKind kind, string brand, string model, string? serial, string? imei)
    {
        var device = new Device { ShopId = owner.ShopId, CustomerId = owner.Id };
        var result = device.Update(kind, brand, model, serial, imei);
        return result.IsSuccess ? device : result.Error!;
    }

    public Result Update(DeviceKind kind, string brand, string model, string? serial, string? imei)
    {
        brand = brand?.Trim() ?? "";
        model = model?.Trim() ?? "";
        serial = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim().ToUpperInvariant();

        if (!Enum.IsDefined(kind))
            return CustomerErrors.InvalidKind;
        if (brand.Length == 0 || brand.Length > MaxBrandLength)
            return CustomerErrors.InvalidBrand;
        if (model.Length == 0 || model.Length > MaxModelLength)
            return CustomerErrors.InvalidModel;
        if (serial?.Length > MaxSerialLength)
            return CustomerErrors.InvalidSerial;

        string? normalizedImei = null;
        if (!string.IsNullOrWhiteSpace(imei))
        {
            normalizedImei = Customers.Imei.Normalize(imei);
            if (normalizedImei is null)
                return CustomerErrors.InvalidImei;
        }

        Kind = kind;
        Brand = brand;
        Model = model;
        Serial = serial;
        Imei = normalizedImei;
        return Result.Success();
    }
}
