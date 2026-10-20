using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Abstractions;

/// <summary>Kullanım senaryolarının domain dışındaki hataları.</summary>
public static class ApplicationErrors
{
    public static readonly Error WorkOrderNotFound = Error.NotFound("work_order.not_found");
    public static readonly Error CustomerNotFound = Error.NotFound("customer.not_found");
    public static readonly Error DeviceNotFound = Error.NotFound("device.not_found");
    public static readonly Error PartNotFound = Error.NotFound("inventory.part_not_found");
    public static readonly Error OwnerOnly = Error.Forbidden("auth.owner_only");
    public static readonly Error DuplicateSku = Error.Conflict("inventory.duplicate_sku");
    public static readonly Error DuplicatePhone = Error.Conflict("customer.duplicate_phone");
}
