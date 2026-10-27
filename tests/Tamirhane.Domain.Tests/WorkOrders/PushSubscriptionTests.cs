using System.Buffers.Text;
using System.Security.Cryptography;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

/// <summary>Tarayıcı bildirimi aboneliği: sadece bilinen push servisleri, geçerli anahtarlar, sınır, kapanmış iş emri.</summary>
public class PushSubscriptionTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(3));
    private const string Fcm = "https://fcm.googleapis.com/fcm/send/abc123";

    private static readonly string P256dh = PublicKey();
    private static readonly string Auth = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

    private static WorkOrder NewOrder() =>
        WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;

    /// <summary>Tarayıcının verdiği gibi: sıkıştırılmamış P-256 açık anahtarı (65 bayt), base64url.</summary>
    private static string PublicKey()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false).Q;
        return Base64Url.EncodeToString([0x04, .. p.X!, .. p.Y!]);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc123")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAAA")]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=BQYAAAB")]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH")]
    public void Browser_push_services_are_accepted(string endpoint)
    {
        var order = NewOrder();

        Assert.True(order.SubscribeToPush(endpoint, P256dh, Auth, Now).IsSuccess);
        Assert.Equal(endpoint, Assert.Single(order.PushSubscriptions).Endpoint);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]             // şifresiz
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc")]       // başka port
    [InlineData("https://localhost/push")]                             // sunucunun kendisi
    [InlineData("https://169.254.169.254/latest/meta-data")]           // bulut makinesinin gizli bilgileri
    [InlineData("https://fcm.googleapis.com.evil.example/x")]          // benzer ad
    [InlineData("https://evilnotify.windows.com/x")]                   // sonek nokta ile başlamalı
    [InlineData("https://user:pass@fcm.googleapis.com/x")]
    [InlineData("fcm.googleapis.com/fcm/send/abc")]
    [InlineData("")]
    public void Other_addresses_are_rejected_so_the_server_cannot_be_pointed_elsewhere(string endpoint)
    {
        var order = NewOrder();

        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, order.SubscribeToPush(endpoint, P256dh, Auth, Now).Error);
        Assert.Empty(order.PushSubscriptions);
    }

    [Fact]
    public void Too_long_endpoint_is_rejected()
    {
        var endpoint = Fcm + new string('a', PushSubscription.MaxEndpointLength);

        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, NewOrder().SubscribeToPush(endpoint, P256dh, Auth, Now).Error);
    }

    [Fact]
    public void Keys_must_be_a_real_public_key_and_a_16_byte_secret()
    {
        var order = NewOrder();
        var notAPoint = Base64Url.EncodeToString(new byte[65]); // 65 bayt ama 0x04 ile başlamıyor

        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, order.SubscribeToPush(Fcm, notAPoint, Auth, Now).Error);
        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, order.SubscribeToPush(Fcm, "kısa", Auth, Now).Error);
        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, order.SubscribeToPush(Fcm, P256dh, "AAAA", Now).Error);
        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, order.SubscribeToPush(Fcm, P256dh, null, Now).Error);
        Assert.Empty(order.PushSubscriptions);
    }

    [Fact]
    public void Padded_keys_are_accepted_and_stored_without_padding()
    {
        var order = NewOrder();

        Assert.True(order.SubscribeToPush(Fcm, P256dh + "=", Auth + "==", Now).IsSuccess);
        Assert.Equal(P256dh, order.PushSubscriptions[0].P256dh);
        Assert.Equal(Auth, order.PushSubscriptions[0].Auth);
    }

    [Fact]
    public void Same_browser_subscribing_again_renews_its_keys_instead_of_adding()
    {
        var order = NewOrder();
        order.SubscribeToPush(Fcm, P256dh, Auth, Now);
        var newKey = PublicKey();

        order.SubscribeToPush(Fcm, newKey, Auth, Now.AddDays(1));

        Assert.Equal(newKey, Assert.Single(order.PushSubscriptions).P256dh);
    }

    [Fact]
    public void When_the_limit_is_reached_the_oldest_subscription_drops()
    {
        var order = NewOrder();
        for (var i = 0; i < WorkOrder.MaxPushSubscriptions; i++)
            order.SubscribeToPush($"{Fcm}/{i}", P256dh, Auth, Now.AddMinutes(i));

        order.SubscribeToPush($"{Fcm}/yeni", P256dh, Auth, Now.AddHours(1));

        Assert.Equal(WorkOrder.MaxPushSubscriptions, order.PushSubscriptions.Count);
        Assert.DoesNotContain(order.PushSubscriptions, s => s.Endpoint == $"{Fcm}/0");
        Assert.Contains(order.PushSubscriptions, s => s.Endpoint == $"{Fcm}/yeni");
    }

    [Fact]
    public void Unsubscribing_removes_only_that_browser_and_unknown_addresses_are_ignored()
    {
        var order = NewOrder();
        order.SubscribeToPush($"{Fcm}/1", P256dh, Auth, Now);
        order.SubscribeToPush($"{Fcm}/2", P256dh, Auth, Now);

        order.UnsubscribeFromPush($"{Fcm}/1");
        order.UnsubscribeFromPush("https://fcm.googleapis.com/bilinmeyen");

        Assert.Equal($"{Fcm}/2", Assert.Single(order.PushSubscriptions).Endpoint);
    }

    [Fact]
    public void A_closed_work_order_takes_no_new_subscriptions()
    {
        var order = NewOrder();
        order.StartDiagnosis(Staff, Now);
        order.StartRepair(Staff, Now);
        order.MarkReady(Staff, Now, new BranchStock(order.BranchId, []));
        order.Deliver(Staff, Now, 90);

        Assert.Equal(WorkOrderErrors.Closed, order.SubscribeToPush(Fcm, P256dh, Auth, Now).Error);
    }
}
