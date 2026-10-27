using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Notifications;

namespace Tamirhane.Infrastructure.Tests.Notifications;

/// <summary>VAPID ayarları ve bildirim içeriği. Docker gerekmez.</summary>
public sealed class WebPushOptionsTests
{
    private static readonly WebPushOptions Valid = PostgresFixture.PushOptions;

    [Fact]
    public void No_settings_means_the_feature_is_off()
    {
        Assert.Null(WebPushOptions.From(null, "", "  "));
    }

    [Fact]
    public void Valid_settings_are_read()
    {
        var options = WebPushOptions.From(Valid.PublicKey + "=", Valid.PrivateKey, " mailto:servis@example.com ");

        Assert.Equal((Valid.PublicKey, Valid.PrivateKey, "mailto:servis@example.com"), (options!.PublicKey, options.PrivateKey, options.Subject));
    }

    [Theory]
    [InlineData("pub", null, "mailto:a@b.c")]          // yarım ayar
    [InlineData("bozuk", "priv", "mailto:a@b.c")]       // açık anahtar bozuk
    [InlineData("pub", "kısa", "mailto:a@b.c")]         // gizli anahtar bozuk
    [InlineData("pub", "priv", "a@b.c")]                // subject biçimi
    public void Partial_or_broken_settings_stop_the_app(string publicKey, string? privateKey, string subject)
    {
        publicKey = publicKey == "pub" ? Valid.PublicKey : publicKey;
        privateKey = privateKey == "priv" ? Valid.PrivateKey : privateKey;

        Assert.Throws<InvalidOperationException>(() => WebPushOptions.From(publicKey, privateKey, subject));
    }

    [Fact]
    public void The_private_key_never_shows_up_in_logs()
    {
        Assert.DoesNotContain(Valid.PrivateKey, Valid.ToString());
        Assert.Contains(Valid.PublicKey, Valid.ToString());
    }

    [Fact]
    public void Payload_has_title_text_and_tracking_link()
    {
        var json = JsonDocument.Parse(CustomerPushNotifications.Payload(
            WorkOrderStatus.Ready, "en", "Apple iPhone 13", new Uri("https://tamirhane.test/t/ABCD"))).RootElement;

        Assert.Equal("Apple iPhone 13: ready for pick-up", json.GetProperty("title").GetString());
        Assert.StartsWith("Your device is ready", json.GetProperty("body").GetString());
        Assert.Equal("https://tamirhane.test/t/ABCD", json.GetProperty("url").GetString());
    }
}

/// <summary>Kuyruktan push servisine kadar: şifreli gönderim, 410'da silme, hata tekrarı, teslimde silme (gerçek PostgreSQL).</summary>
[Collection(PostgresCollection.Name)]
public sealed class CustomerPushDeliveryTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(3));

    [DockerFact]
    public async Task Every_subscribed_browser_gets_an_encrypted_notification_it_can_read()
    {
        using var phone = Browser("telefon");
        using var laptop = Browser("bilgisayar");
        var (shopId, order) = await SeedAsync(phone, laptop);

        await MoveAsync(shopId, order.Id, o => o.StartDiagnosis(Guid.NewGuid(), Now));
        await NotifyAsync(shopId, order.Id, WorkOrderStatus.Diagnosing);

        foreach (var browser in new[] { phone, laptop })
        {
            var request = Assert.Single(db.Push.To(browser.Endpoint));
            var payload = JsonDocument.Parse(browser.Decrypt(request.Body)).RootElement;
            Assert.Equal("Apple iPhone 13: inceleniyor", payload.GetProperty("title").GetString());
            Assert.EndsWith($"/t/{order.TrackingCode}", payload.GetProperty("url").GetString());

            Assert.StartsWith("vapid t=", request.Authorization);
            Assert.Contains($"k={PostgresFixture.PushOptions.PublicKey}", request.Authorization);
            Assert.Equal("aes128gcm", request.ContentEncoding);
            Assert.Equal("86400", request.Ttl);
            Assert.Equal(order.Id.ToString("N"), request.Topic); // yenisi eskisinin yerine geçer
        }
        Assert.Equal(2, await SubscriptionCountAsync(order.Id));
    }

    [DockerFact]
    public async Task A_subscription_the_push_service_says_is_gone_is_deleted()
    {
        using var gone = Browser("izni-kaldirildi");
        using var kept = Browser("hala-abone");
        var (shopId, order) = await SeedAsync(gone, kept);
        db.Push.Answer(gone.Endpoint, HttpStatusCode.Gone);

        await MoveAsync(shopId, order.Id, o => o.StartDiagnosis(Guid.NewGuid(), Now));
        await NotifyAsync(shopId, order.Id, WorkOrderStatus.Diagnosing);

        using var check = db.AsShop(Guid.Empty);
        var left = await check.Db.Set<PushSubscription>().Where(s => s.WorkOrderId == order.Id).Select(s => s.Endpoint).ToListAsync();
        Assert.Equal([kept.Endpoint], left);
    }

    [DockerFact]
    public async Task A_failing_push_service_is_retried_and_the_subscription_is_kept()
    {
        using var browser = Browser("gecici-hata");
        var (shopId, order) = await SeedAsync(browser);
        db.Push.Answer(browser.Endpoint, HttpStatusCode.ServiceUnavailable);

        await MoveAsync(shopId, order.Id, o => o.StartDiagnosis(Guid.NewGuid(), Now));
        await NotifyAsync(shopId, order.Id, WorkOrderStatus.Diagnosing);

        Assert.Equal(3, db.Push.To(browser.Endpoint).Count); // arka plan kuyruğu 3 kez dener
        Assert.Equal(1, await SubscriptionCountAsync(order.Id));
    }

    [DockerFact]
    public async Task After_the_last_news_of_a_closed_work_order_its_subscriptions_are_deleted()
    {
        using var browser = Browser("teslim");
        var (shopId, order) = await SeedAsync(browser);

        await MoveAsync(shopId, order.Id, o =>
        {
            var staff = Guid.NewGuid();
            o.StartDiagnosis(staff, Now);
            o.StartRepair(staff, Now);
            o.MarkReady(staff, Now, new BranchStock(o.BranchId, []));
            return o.Deliver(staff, Now, 90);
        });
        await NotifyAsync(shopId, order.Id, WorkOrderStatus.Delivered);

        var payload = JsonDocument.Parse(browser.Decrypt(Assert.Single(db.Push.To(browser.Endpoint)).Body)).RootElement;
        Assert.Equal("Apple iPhone 13: teslim edildi", payload.GetProperty("title").GetString());
        Assert.Equal(0, await SubscriptionCountAsync(order.Id));
    }

    [DockerFact]
    public async Task An_outdated_status_is_not_sent()
    {
        using var browser = Browser("eski-durum");
        var (shopId, order) = await SeedAsync(browser);

        await MoveAsync(shopId, order.Id, o => o.StartDiagnosis(Guid.NewGuid(), Now));
        await NotifyAsync(shopId, order.Id, WorkOrderStatus.Received); // kuyrukta beklerken iş emri ilerledi

        Assert.Empty(db.Push.To(browser.Endpoint));
    }

    private static TestBrowser Browser(string name) => new($"https://fcm.googleapis.com/fcm/send/{name}-{Guid.NewGuid():N}");

    private async Task NotifyAsync(Guid shopId, Guid orderId, WorkOrderStatus status)
    {
        using (var scope = db.AsShop(shopId))
            scope.Get<INotificationQueue>().Enqueue(new WorkOrderNotification(shopId, orderId, status));
        await db.IdleAsync();
    }

    private async Task MoveAsync(Guid shopId, Guid orderId, Func<WorkOrder, Result> step)
    {
        using var own = db.AsShop(shopId);
        var order = (await own.Orders.GetAsync(orderId, default))!;
        Assert.True(step(order).IsSuccess);
        await own.UnitOfWork.SaveChangesAsync(default);
    }

    private async Task<int> SubscriptionCountAsync(Guid orderId)
    {
        using var scope = db.AsShop(Guid.Empty);
        return await scope.Db.Set<PushSubscription>().CountAsync(s => s.WorkOrderId == orderId);
    }

    private async Task<(Guid ShopId, WorkOrder Order)> SeedAsync(params TestBrowser[] browsers)
    {
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;
        var branch = Branch.Create(shop.Id, "Merkez", "Moda Cd. 5", Now, "0216 345 67 89").Value;
        var phone = $"0532 {Random.Shared.Next(100, 999)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)}";
        var customer = Customer.Create(shop.Id, "Ayşe Kaya", phone, null, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        using (var anonymous = db.AsShop(Guid.Empty))
        {
            anonymous.Db.AddRange(shop, branch, customer, device);
            await anonymous.Db.SaveChangesAsync();
        }

        using var own = db.AsShop(shop.Id);
        var order = WorkOrder.Receive(shop.Id, branch.Id, "2026-0001", TrackingCode.Generate(), customer.Id, device.Id,
            "Ekran kırık", null, null, null, own.User.StaffId, Now).Value;
        foreach (var browser in browsers)
            Assert.True(order.SubscribeToPush(browser.Endpoint, browser.P256dh, browser.Auth, Now).IsSuccess);
        own.Orders.Add(order);
        await own.UnitOfWork.SaveChangesAsync(default);
        return (shop.Id, order);
    }
}
