using Microsoft.EntityFrameworkCore;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Sahip atölyesini Ayarlar'dan siler: şifre yeniden sorulur, bütün veri ve hesaplar gider, başka atölye etkilenmez.</summary>
[Collection(WebCollection.Name)]
public sealed class ShopDeletionFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Wrong_password_keeps_the_shop()
    {
        var owner = await _flows.OwnerAsync("sil-yanlis@example.com");
        await ReceiveAsync(owner, "0532 111 22 33");

        var response = await owner.SubmitAsync("/settings", "/settings/delete", new() { ["password"] = "yanlis-sifre-1" });

        Assert.Equal("/settings", Browser.RedirectOf(response));
        Assert.Contains("Şifre yanlış; atölye silinmedi.", await (await owner.GetAsync("/settings")).Content.ReadAsStringAsync());
        Assert.Equal((1, 1), await CountsAsync("sil-yanlis@example.com"));
    }

    [DockerFact]
    public async Task Owner_deletes_the_shop_with_all_its_data_and_accounts()
    {
        var owner = await _flows.OwnerAsync("sil-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "sil-usta@example.com");
        await ReceiveAsync(owner, "0532 444 55 66");
        var shopId = await ShopIdAsync("sil-sahip@example.com");
        var other = await _flows.OwnerAsync("sil-komsu@example.com");
        await ReceiveAsync(other, "0532 777 88 99");

        var response = await owner.SubmitAsync("/settings", "/settings/delete", new() { ["password"] = OwnerPassword });

        Assert.Equal("/", Browser.RedirectOf(response));
        Assert.Contains("Atölyen ve bütün verisi silindi.", await (await owner.GetAsync("/")).Content.ReadAsStringAsync());
        Assert.StartsWith("/account/login", Browser.RedirectOf(await owner.GetAsync("/panel")));
        Assert.StartsWith("/account/login", Browser.RedirectOf(await technician.GetAsync("/panel")));
        var login = await app.NewBrowser().SubmitAsync("/account/login", "/account/login",
            new() { ["Email"] = "sil-sahip@example.com", ["Password"] = OwnerPassword });
        Assert.Contains("E-posta ya da şifre hatalı.", await login.Content.ReadAsStringAsync());

        await app.AsShopAsync(shopId, async db =>
        {
            Assert.False(await db.Shops.IgnoreQueryFilters().AnyAsync(s => s.Id == shopId));
            Assert.False(await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.ShopId == shopId));
            Assert.False(await db.WorkOrders.IgnoreQueryFilters().AnyAsync(o => o.ShopId == shopId));
            Assert.False(await db.Users.AnyAsync(u => u.Email == "sil-usta@example.com"));
        });
        Assert.Equal((1, 1), await CountsAsync("sil-komsu@example.com")); // komşu atölye yerinde
    }

    [DockerFact]
    public async Task Technician_cannot_delete_the_shop()
    {
        var owner = await _flows.OwnerAsync("sil-yetki@example.com");
        var technician = await _flows.TechnicianAsync(owner, "sil-yetki-usta@example.com");

        // Ayarlar sayfası teknisyene kapalı; token başka sayfadan alınır, kural sunucuda.
        var response = await technician.SubmitAsync("/panel", "/settings/delete", new() { ["password"] = TechnicianPassword });

        Assert.NotEqual("/", response.Headers.Location?.OriginalString);
        Assert.Equal(1, (await CountsAsync("sil-yetki@example.com")).Shops);
    }

    private async Task<Guid> ShopIdAsync(string ownerEmail)
    {
        var shopId = Guid.Empty;
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            shopId = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.ShopId).SingleAsync();
        });
        return shopId;
    }

    private async Task<(int Shops, int WorkOrders)> CountsAsync(string ownerEmail)
    {
        var shopId = await ShopIdAsync(ownerEmail);
        var counts = (0, 0);
        await app.AsShopAsync(shopId, async db =>
            counts = (await db.Shops.CountAsync(), await db.WorkOrders.CountAsync()));
        return counts;
    }
}
