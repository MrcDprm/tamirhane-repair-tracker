using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>Şubeler ve atölye ayarları gerçek PostgreSQL ile: kayıtta ilk şube, kiracı filtresi, oturumdaki şube.</summary>
[Collection(PostgresCollection.Name)]
public sealed class BranchPersistenceTests(PostgresFixture db)
{
    private const string Password = "mavi-tornavida-17";

    [DockerFact]
    public async Task Registration_opens_the_first_branch_and_other_shops_cannot_see_it()
    {
        var shop = await RegisterAsync("sube-ilk@example.com", "en");

        using var own = db.AsShop(shop.ShopId);
        var branch = await own.Db.Branches.SingleAsync();
        Assert.Equal("Main branch", branch.Name);
        Assert.Equal(branch.Id, (await own.Db.Staff.SingleAsync()).BranchId);

        using var other = db.AsShop(Guid.NewGuid());
        Assert.Null(await other.Get<IBranchRepository>().GetAsync(branch.Id, default));
        Assert.Empty(await other.Get<IBranchRepository>().ListAsync(default));
    }

    [DockerFact]
    public async Task Branch_cannot_be_added_to_another_shop()
    {
        var victim = await RegisterAsync("sube-kurban@example.com");

        using var attacker = db.AsShop(Guid.NewGuid());
        attacker.Get<IBranchRepository>().Add(Branch.Create(victim.ShopId, "Sızma", null, DateTimeOffset.UtcNow).Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() => attacker.UnitOfWork.SaveChangesAsync(default));
    }

    [DockerFact]
    public async Task Switching_branch_changes_the_branch_in_the_next_session_principal()
    {
        var shop = await RegisterAsync("sube-gecis@example.com");
        Guid second;
        using (var owner = OwnerScope(shop))
        {
            second = (await new AddBranch(owner.Get<IBranchRepository>(), owner.UnitOfWork, owner.User, TimeProvider.System)
                .HandleAsync(new("Kadıköy", "Moda Cd. 12"), default)).Value;
            var switched = await new SwitchBranch(owner.Get<IBranchRepository>(), owner.Get<IStaffRepository>(), owner.UnitOfWork, owner.User)
                .HandleAsync(second, default);
            Assert.True(switched.IsSuccess);
        }

        // Güvenlik damgası her istekte doğrulanınca oturum bu fabrikayla yeniden kurulur.
        using var scope = db.AsShop(Guid.Empty);
        var user = (await scope.Users.FindByEmailAsync("sube-gecis@example.com"))!;
        var principal = await scope.Get<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        var current = new HttpCurrentUser(new FixedAccessor(new DefaultHttpContext { User = principal }));
        Assert.Equal(second, current.BranchId);
    }

    [DockerFact]
    public async Task Shop_settings_are_read_by_business_rules_after_update()
    {
        var shop = await RegisterAsync("ayar@example.com");

        using (var owner = OwnerScope(shop))
        {
            var updated = await new UpdateShopSettings(owner.Get<IShopRepository>(), owner.UnitOfWork, owner.User)
                .HandleAsync(new("Kaya Teknik", 180, 10m, "tr"), default);
            Assert.True(updated.IsSuccess);
        }

        using var check = db.AsShop(shop.ShopId);
        Assert.Equal(new ShopSettings(180, 10m), await check.Get<IShopSettings>().GetAsync(shop.ShopId, default));
    }

    private sealed record RegisteredShop(Guid ShopId, Guid OwnerStaffId, Guid BranchId);

    private async Task<RegisteredShop> RegisterAsync(string ownerEmail, string locale = "tr")
    {
        using (var anonymous = db.AsShop(Guid.Empty))
        {
            var register = new RegisterShop(anonymous.Accounts, anonymous.Get<IShopRepository>(), anonymous.UnitOfWork, TimeProvider.System);
            Assert.True((await register.HandleAsync(new("Kaya Telefon", "Ayşe Kaya", ownerEmail, Password, locale), default)).IsSuccess);
        }

        using var lookup = db.AsShop(Guid.Empty);
        var user = (await lookup.Users.FindByEmailAsync(ownerEmail))!;
        var owner = await lookup.Db.Staff.IgnoreQueryFilters().SingleAsync(s => s.UserId == user.Id);
        return new RegisteredShop(owner.ShopId, owner.Id, owner.BranchId);
    }

    private ShopScope OwnerScope(RegisteredShop shop)
    {
        var scope = db.AsShop(shop.ShopId);
        var user = scope.Get<TestUser>();
        user.StaffId = shop.OwnerStaffId;
        user.BranchId = shop.BranchId;
        return scope;
    }

    private sealed class FixedAccessor(HttpContext? context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
