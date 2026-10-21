using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Web.Tests;

/// <summary>Uygulama gerçek servis kayıtlarıyla açılıyor ve veritabanına doğru bağlanıyor.</summary>
[Collection(WebCollection.Name)]
public sealed class WebHostTests(TamirhaneWebFactory app)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public async Task Health_endpoint_reports_a_reachable_database()
    {
        var response = await app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_page_opens()
    {
        var response = await app.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Visitor_without_a_session_sees_no_shop_records()
    {
        var shopId = Guid.NewGuid();
        await app.AsShopAsync(shopId, async db =>
        {
            db.Customers.Add(Customer.Create(shopId, "Ayşe Kaya", "0532 555 55 55", null, true, Now).Value);
            await db.SaveChangesAsync();
        });

        // Uygulamanın kendi kaydı: oturum yokken atölye boştur, filtre hiçbir müşteri döndürmez.
        using var scope = app.Services.CreateScope();
        Assert.Equal(Guid.Empty, scope.ServiceProvider.GetRequiredService<ICurrentUser>().ShopId);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<TamirhaneDbContext>().Customers.ToListAsync());

        await app.AsShopAsync(shopId, async db => Assert.Single(await db.Customers.ToListAsync()));
    }
}
