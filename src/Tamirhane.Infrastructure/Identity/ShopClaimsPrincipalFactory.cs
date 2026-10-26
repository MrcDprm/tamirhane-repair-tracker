using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>Oturum çerezindeki atölye bilgileri.</summary>
internal static class ShopClaims
{
    public const string ShopId = "tamirhane:shop";
    public const string StaffId = "tamirhane:staff";
    public const string BranchId = "tamirhane:branch";
    public const string DisplayName = "tamirhane:name";
}

/// <summary>
/// Oturum açılırken (ve güvenlik damgası her istekte doğrulanırken) hesabın personel kaydını bulur;
/// atölye, personel, şube, rol ve görünen ad çereze yazılır. Personel kaydı yoksa oturumun atölyesi olmaz.
/// Damga her istekte doğrulandığı için bilgiler her istekte yeniden okunur: şube değişince bir sonraki istekte görünür.
/// </summary>
internal sealed class ShopClaimsPrincipalFactory(
    UserManager<ApplicationUser> users, IOptions<IdentityOptions> options, TamirhaneDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Bu noktada oturumun atölyesi henüz bilinmiyor; kiracı filtresi bilerek kapatılır, arama hesap kimliğiyle.
        var staff = await db.Staff.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s => s.UserId == user.Id);
        if (staff is not null)
        {
            identity.AddClaim(new Claim(ShopClaims.ShopId, staff.ShopId.ToString()));
            identity.AddClaim(new Claim(ShopClaims.StaffId, staff.Id.ToString()));
            identity.AddClaim(new Claim(ShopClaims.BranchId, staff.BranchId.ToString()));
            identity.AddClaim(new Claim(ShopClaims.DisplayName, staff.DisplayName));
            identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, staff.Role.ToString()));
        }
        return identity;
    }
}
