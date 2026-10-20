namespace Tamirhane.Application.Abstractions;

/// <summary>
/// İsteği yapan personel. Atölye ve personel kimliği her zaman oturumdan gelir, asla formdan alınmaz;
/// böylece bir atölye başka atölyenin verisine yazamaz.
/// </summary>
public interface ICurrentUser
{
    Guid ShopId { get; }
    Guid StaffId { get; }
    bool IsOwner { get; }
}
