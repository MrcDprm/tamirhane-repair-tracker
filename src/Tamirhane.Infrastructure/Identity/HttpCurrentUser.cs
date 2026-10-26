using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// İsteği yapan personel, oturum çerezinden. Oturum yoksa (ya da istek dışında) atölyesi Guid.Empty olur
/// ve kiracı filtresi hiçbir kayıt döndürmez.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid ShopId => Read(ShopClaims.ShopId);
    public Guid StaffId => Read(ShopClaims.StaffId);
    public Guid BranchId => Read(ShopClaims.BranchId);
    public bool IsOwner => User?.IsInRole(nameof(StaffRole.Owner)) == true;

    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    private Guid Read(string claimType) =>
        Guid.TryParse(User?.FindFirstValue(claimType), out var id) ? id : Guid.Empty;
}
