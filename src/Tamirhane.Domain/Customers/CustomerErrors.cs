using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Customers;

public static class CustomerErrors
{
    public static readonly Error InvalidName = Error.Validation("customer.invalid_name");
    public static readonly Error InvalidPhone = Error.Validation("customer.invalid_phone");
    public static readonly Error InvalidEmail = Error.Validation("customer.invalid_email");
    public static readonly Error ConsentRequired = Error.Validation("customer.consent_required");
    public static readonly Error InvalidKind = Error.Validation("device.invalid_kind");
    public static readonly Error InvalidBrand = Error.Validation("device.invalid_brand");
    public static readonly Error InvalidModel = Error.Validation("device.invalid_model");
    public static readonly Error InvalidSerial = Error.Validation("device.invalid_serial");
    public static readonly Error InvalidImei = Error.Validation("device.invalid_imei");
}
