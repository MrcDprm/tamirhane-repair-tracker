using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>Atölye kaydı oturumsuz yapılır; sonra atölye ve personeli sadece kendi oturumunda görünür.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ShopPersistenceTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(3));

    [DockerFact]
    public async Task New_shop_with_owner_and_account_is_saved_without_a_session()
    {
        var (shop, staff) = await RegisterAsync("kaya@example.com");

        using var own = db.AsShop(shop.Id);
        Assert.Equal("Kaya Telefon", (await own.Db.Shops.SingleAsync()).Name);
        Assert.Equal(staff.Id, (await own.Db.Staff.SingleAsync()).Id);

        using var other = db.AsShop(Guid.NewGuid());
        Assert.Empty(await other.Db.Shops.ToListAsync());
        Assert.Empty(await other.Db.Staff.ToListAsync());
    }

    [DockerFact]
    public async Task Staff_cannot_be_added_to_an_existing_shop_from_another_session()
    {
        var (shop, _) = await RegisterAsync("demir@example.com");

        using var anonymous = db.AsShop(Guid.Empty);
        var user = new ApplicationUser { UserName = "yabanci@example.com", Email = "yabanci@example.com" };
        anonymous.Db.Users.Add(user);
        anonymous.Db.Staff.Add(Staff.Create(shop.Id, user.Id, StaffRole.Owner, "Yabancı").Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.Db.SaveChangesAsync());
    }

    [DockerFact]
    public async Task Email_is_unique_even_without_the_identity_check()
    {
        await RegisterAsync("ayni@example.com");

        await Assert.ThrowsAsync<DbUpdateException>(() => RegisterAsync("ayni@example.com"));
    }

    [DockerFact]
    public async Task Uncommitted_transaction_leaves_nothing_behind()
    {
        var user = new ApplicationUser { UserName = "yarim@example.com", Email = "yarim@example.com" };
        var shop = Shop.Create("Yarım Kayıt", "tr", Now).Value;

        using (var anonymous = db.AsShop(Guid.Empty))
        {
            await using var transaction = await anonymous.UnitOfWork.BeginTransactionAsync(default);
            anonymous.Db.Users.Add(user);
            await anonymous.UnitOfWork.SaveChangesAsync(default); // Identity'nin hesabı ayrı kaydetmesi gibi
            anonymous.Db.AddRange(shop, Staff.Create(shop.Id, user.Id, StaffRole.Owner, "Ayşe Kaya").Value);
            await anonymous.UnitOfWork.SaveChangesAsync(default);
            // CommitAsync yok: ör. e-posta gönderilemeden istek düştü
        }

        using var check = db.AsShop(shop.Id);
        Assert.False(await check.Db.Users.AnyAsync(u => u.Id == user.Id));
        Assert.Empty(await check.Db.Shops.ToListAsync());
    }

    private async Task<(Shop Shop, Staff Staff)> RegisterAsync(string email)
    {
        using var anonymous = db.AsShop(Guid.Empty);
        var user = new ApplicationUser
        {
            UserName = email, NormalizedUserName = email.ToUpperInvariant(),
            Email = email, NormalizedEmail = email.ToUpperInvariant(),
        };
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;
        var staff = Staff.Create(shop.Id, user.Id, StaffRole.Owner, "Ayşe Kaya").Value;
        anonymous.Db.AddRange(user, shop, staff);
        await anonymous.Db.SaveChangesAsync();
        return (shop, staff);
    }
}
