using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>Takip sayfasından gelen abonelik oturumsuz kaydedilir, iş emriyle yüklenir, iş emriyle silinir.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PushSubscriptionPersistenceTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(3));
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/kalici";

    [DockerFact]
    public async Task Subscription_from_the_tracking_page_is_saved_without_a_session_and_loaded_with_the_order()
    {
        var (shopId, code) = await SeedAsync();
        var (p256dh, auth) = Keys();

        using (var anonymous = db.AsShop(Guid.Empty))
        {
            var order = (await anonymous.Orders.GetByTrackingCodeAsync(code, default))!;
            Assert.True(order.SubscribeToPush(Endpoint, p256dh, auth, Now).IsSuccess);
            await anonymous.UnitOfWork.SaveChangesAsync(default);
        }

        using var own = db.AsShop(shopId);
        var saved = (await own.Orders.GetByTrackingCodeAsync(code, default))!;
        var subscription = Assert.Single(saved.PushSubscriptions);
        Assert.Equal((Endpoint, p256dh, auth), (subscription.Endpoint, subscription.P256dh, subscription.Auth));
    }

    [DockerFact]
    public async Task Subscriptions_go_away_with_their_work_order()
    {
        var (shopId, code) = await SeedAsync();
        var (p256dh, auth) = Keys();
        Guid orderId;
        using (var own = db.AsShop(shopId))
        {
            var order = (await own.Orders.GetByTrackingCodeAsync(code, default))!;
            orderId = order.Id;
            order.SubscribeToPush(Endpoint, p256dh, auth, Now);
            await own.UnitOfWork.SaveChangesAsync(default);

            await own.Db.WorkOrders.Where(o => o.Id == orderId).ExecuteDeleteAsync();
        }

        using var check = db.AsShop(shopId);
        Assert.False(await check.Db.Set<PushSubscription>().AnyAsync(s => s.WorkOrderId == orderId));
    }

    private static (string P256dh, string Auth) Keys()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        return (Base64Url.EncodeToString([0x04, .. q.X!, .. q.Y!]), Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)));
    }

    private async Task<(Guid ShopId, string Code)> SeedAsync()
    {
        var shopId = Guid.NewGuid();
        using var shop = db.AsShop(shopId);
        var phone = $"0532 {Random.Shared.Next(100, 999)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)}";
        var customer = Customer.Create(shopId, "Ayşe Kaya", phone, null, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        var order = WorkOrder.Receive(shopId, Guid.NewGuid(), "2026-0001", TrackingCode.Generate(),
            customer.Id, device.Id, "Ekran kırık", null, null, null, shop.User.StaffId, Now).Value;
        shop.Customers.Add(customer);
        shop.Db.Devices.Add(device);
        shop.Orders.Add(order);
        await shop.UnitOfWork.SaveChangesAsync(default);
        return (shopId, order.TrackingCode);
    }
}
