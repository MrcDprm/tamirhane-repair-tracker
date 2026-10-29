using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>
/// Takip sayfasının oturumsuz okuması kiracı filtresini kapatır; onun yerine bütün kimliklerin aynı atölyeye ait olması
/// şartı vardır. Başka atölyenin müşterisi ya da cihazı karıştırılırsa sonuç boştur.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TrackingDirectoryTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(3));

    [DockerFact]
    public async Task Reads_shop_branch_customer_and_device_without_a_session()
    {
        var a = await SeedAsync("Kaya Telefon", "0532 501 01 01");

        using var anonymous = db.AsShop(Guid.Empty);
        var parties = await anonymous.Get<ITrackingDirectory>().GetAsync(a.Shop, a.Branch, a.Customer, a.Device, default);

        Assert.Equal(new TrackingParties("Kaya Telefon", "Merkez", "Moda Cd.", "+902163456789", "Ayşe Kaya", DeviceKind.Phone, "Apple", "iPhone 13", "tr"), parties);
    }

    [DockerFact]
    public async Task Ids_from_another_shop_find_nothing()
    {
        var a = await SeedAsync("Kaya Telefon", "0532 502 02 02");
        var b = await SeedAsync("Demir Teknik", "0532 503 03 03");

        using var anonymous = db.AsShop(Guid.Empty);
        var directory = anonymous.Get<ITrackingDirectory>();
        Assert.Null(await directory.GetAsync(a.Shop, a.Branch, b.Customer, b.Device, default));
        Assert.Null(await directory.GetAsync(a.Shop, b.Branch, a.Customer, a.Device, default));
        Assert.Null(await directory.GetAsync(a.Shop, a.Branch, a.Customer, b.Device, default));
    }

    private async Task<(Guid Shop, Guid Branch, Guid Customer, Guid Device)> SeedAsync(string shopName, string phone)
    {
        using var anonymous = db.AsShop(Guid.Empty);
        var shop = Shop.Create(shopName, "tr", Now).Value;
        var branch = Branch.Create(shop.Id, "Merkez", "Moda Cd.", Now, "0216 345 67 89").Value;
        var customer = Customer.Create(shop.Id, "Ayşe Kaya", phone, null, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        anonymous.Db.AddRange(shop, branch, customer, device);
        await anonymous.Db.SaveChangesAsync();
        return (shop.Id, branch.Id, customer.Id, device.Id);
    }
}
