using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Identity;

/// <summary>Girişte oluşan oturum atölyeyi, personeli ve rolü taşır; HttpCurrentUser bunları geri okur.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionClaimsTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(3));

    [DockerFact]
    public async Task Session_carries_shop_staff_and_role()
    {
        var (shop, staff, user) = await RegisterAsync("oturum@example.com", StaffRole.Owner);

        var current = await CurrentUserFor(user);

        Assert.Equal(shop.Id, current.ShopId);
        Assert.Equal(staff.Id, current.StaffId);
        Assert.Equal(staff.BranchId, current.BranchId);
        Assert.True(current.IsOwner);
    }

    [DockerFact]
    public async Task Technician_session_is_not_owner()
    {
        var (_, _, user) = await RegisterAsync("teknisyen@example.com", StaffRole.Technician);

        Assert.False((await CurrentUserFor(user)).IsOwner);
    }

    [DockerFact]
    public async Task Account_without_staff_record_has_no_shop()
    {
        using var scope = db.AsShop(Guid.Empty);
        var user = new ApplicationUser { UserName = "personelsiz@example.com", Email = "personelsiz@example.com" };
        Assert.True((await scope.Users.CreateAsync(user)).Succeeded);

        var current = await CurrentUserFor(user);

        Assert.Equal(Guid.Empty, current.ShopId);
        Assert.Equal(Guid.Empty, current.StaffId);
    }

    [Fact]
    public void No_request_means_no_shop()
    {
        var current = new HttpCurrentUser(new FixedAccessor(null));

        Assert.Equal(Guid.Empty, current.ShopId);
        Assert.False(current.IsOwner);
    }

    private async Task<HttpCurrentUser> CurrentUserFor(ApplicationUser user)
    {
        using var scope = db.AsShop(Guid.Empty);
        var factory = scope.Get<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var principal = await factory.CreateAsync(user);
        return new HttpCurrentUser(new FixedAccessor(new DefaultHttpContext { User = principal }));
    }

    private async Task<(Shop Shop, Staff Staff, ApplicationUser User)> RegisterAsync(string email, StaffRole role)
    {
        using var scope = db.AsShop(Guid.Empty);
        var user = new ApplicationUser { UserName = email, Email = email };
        Assert.True((await scope.Users.CreateAsync(user)).Succeeded);
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;
        var branch = Branch.Create(shop.Id, "Merkez", null, Now).Value;
        var staff = Staff.Create(shop.Id, user.Id, role, "Ayşe Kaya", branch.Id).Value;
        scope.Db.AddRange(shop, branch, staff);
        await scope.Db.SaveChangesAsync();
        return (shop, staff, user);
    }

    // HttpContextAccessor değeri AsyncLocal'da tutar; async yardımcı metotta atanan değer çağırana geri akmaz.
    private sealed class FixedAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
