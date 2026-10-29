using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Demo;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>
/// Demo atölyeler gerçek PostgreSQL'de: oturumsuz eklenir (stok kayıtları kendi atölyesinde), süresi dolunca bütün
/// tablolardan ve hesaplarıyla silinir; süresi dolmayan demo ve gerçek atölyeler dokunulmadan kalır.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DemoShopsTests(PostgresFixture db)
{
    // Atölyeye bağlı kayıt tutan bütün tablolar ve atölyeyi bulma yolu (alt kayıtlar iş emri üzerinden).
    private static readonly string[] Tables =
    [
        "SELECT count(*)::int AS \"Value\" FROM shops WHERE id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM branches WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM staff WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM customers WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM devices WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM parts WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM stock_levels WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM stock_movements WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM work_orders WHERE shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM status_changes c JOIN work_orders o ON o.id = c.work_order_id WHERE o.shop_id = {0}",
        "SELECT count(*)::int AS \"Value\" FROM work_order_sequences WHERE shop_id = {0}",
    ];

    [DockerFact]
    public async Task Sample_stock_is_saved_under_the_demo_shop()
    {
        var (shopId, _) = await SeedDemoAsync(DateTimeOffset.Now);

        using var scope = db.AsShop(shopId);
        Assert.Equal(6, await scope.Db.StockLevels.CountAsync()); // kiracı filtresiyle, atölyenin kendi oturumundan
        Assert.Equal(12, await scope.Db.WorkOrders.CountAsync());
    }

    [DockerFact]
    public async Task Expired_demo_is_removed_from_every_table_with_its_accounts()
    {
        var (expired, expiredUsers) = await SeedDemoAsync(DateTimeOffset.Now - Shop.DemoLifetime - TimeSpan.FromHours(1));
        var (fresh, freshUsers) = await SeedDemoAsync(DateTimeOffset.Now);

        using (var scope = db.AsShop(Guid.Empty))
            Assert.True(await new DeleteExpiredDemos(scope.Get<IDemoShops>(), TimeProvider.System).HandleAsync(default) >= 1);

        using var check = db.AsShop(Guid.Empty);
        foreach (var sql in Tables)
        {
            Assert.Equal(0, await CountAsync(check, sql, expired));
            Assert.NotEqual(0, await CountAsync(check, sql, fresh));
        }
        foreach (var user in expiredUsers)
            Assert.Null(await check.Users.FindByIdAsync(user.ToString()));
        foreach (var user in freshUsers)
            Assert.NotNull(await check.Users.FindByIdAsync(user.ToString()));
    }

    [DockerFact]
    public async Task A_real_shop_is_never_deleted_as_a_demo()
    {
        var shop = Shop.Create("Kaya Telefon", "tr", DateTimeOffset.Now.AddDays(-30)).Value;
        var branch = Branch.Create(shop.Id, "Merkez", null, shop.CreatedAt).Value;
        using (var add = db.AsShop(Guid.Empty))
        {
            add.Db.AddRange(shop, branch);
            await add.Db.SaveChangesAsync();
        }

        using var scope = db.AsShop(Guid.Empty);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Get<IDemoShops>().DeleteAsync(shop.Id, default));
        Assert.Contains("Sadece demo", refused.Message);
        Assert.DoesNotContain(shop.Id, await scope.Get<IDemoShops>().ListOpenedBeforeAsync(DateTimeOffset.Now, 1000, default));
        Assert.Equal(1, await CountAsync(scope, Tables[0], shop.Id));
        Assert.Equal(1, await CountAsync(scope, Tables[1], shop.Id));
    }

    private async Task<(Guid ShopId, Guid[] Users)> SeedDemoAsync(DateTimeOffset openedAt)
    {
        using var scope = db.AsShop(Guid.Empty);
        var owner = (await scope.Accounts.CreateConfirmedAsync(DemoData.Email("owner"), DemoData.Password(), default)).Value;
        var tech = (await scope.Accounts.CreateConfirmedAsync(DemoData.Email("tech"), DemoData.Password(), default)).Value;
        var demo = DemoData.Build("tr", owner, tech, openedAt);
        scope.Get<IDemoShops>().Add(demo);
        await scope.UnitOfWork.SaveChangesAsync(default);
        return (demo.Shop.Id, [owner, tech]);
    }

    private static async Task<int> CountAsync(ShopScope scope, string sql, Guid shopId) =>
        (await scope.Db.Database.SqlQueryRaw<int>(sql, shopId).ToListAsync())[0];
}
