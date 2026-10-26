using Tamirhane.Domain.Shops;

namespace Tamirhane.Web.Authorization;

/// <summary>
/// Sayfa yetkileri. Rol oturum çerezinde (girişte personel kaydından); güvenlik damgası her istekte doğrulandığı
/// için çıkarılan personelin oturumu da hemen düşer.
/// </summary>
public static class Policies
{
    /// <summary>Ayarlar, personel ve ciro: teknisyen giremez, access-denied sayfasına yönlendirilir.</summary>
    public const string OwnerOnly = "owner-only";

    public static IServiceCollection AddShopPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerOnly, policy => policy.RequireAuthenticatedUser().RequireRole(nameof(StaffRole.Owner)));
        return services;
    }
}
