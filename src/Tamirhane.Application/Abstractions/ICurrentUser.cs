namespace Tamirhane.Application.Abstractions;

/// <summary>
/// İsteği yapan personel. Atölye ve personel kimliği her zaman oturumdan gelir, asla formdan alınmaz;
/// böylece bir atölye başka atölyenin verisine yazamaz.
/// </summary>
public interface ICurrentUser
{
    Guid ShopId { get; }
    Guid StaffId { get; }
    /// <summary>Personelin çalıştığı şube: yeni iş emirleri ve stok işlemleri buraya yazılır.</summary>
    Guid BranchId { get; }
    bool IsOwner { get; }
}
