using Tamirhane.Application.Abstractions;

namespace Tamirhane.Web.Auth;

/// <summary>
/// Oturum açmamış ziyaretçi. Atölyesi yok (Guid.Empty), bu yüzden atölye filtresi hiçbir kayıt döndürmez.
/// Giriş sistemi (#16) gelince yerini oturumdaki personeli okuyan sınıf alır.
/// </summary>
public sealed class AnonymousUser : ICurrentUser
{
    public Guid ShopId => Guid.Empty;
    public Guid StaffId => Guid.Empty;
    public bool IsOwner => false;
}
