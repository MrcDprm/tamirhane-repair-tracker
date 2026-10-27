using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>Takip sayfasından abone olma ve çıkma: koddan bulunur, geçersiz abonelik kaydedilmez.</summary>
public class PushSubscriptionHandlerTests
{
    private const string Code = "7K3M9QX2HFDA";
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc123";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.FromHours(3));

    private readonly FakeWorkOrders _orders = new(new FakeUser(Guid.NewGuid()));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly WorkOrder _order;
    private readonly PushSubscriptionInput _valid;

    public PushSubscriptionHandlerTests()
    {
        _order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0042", Code, Guid.NewGuid(), Guid.NewGuid(),
            "Ekran kırık", null, null, null, Guid.NewGuid(), Now).Value;
        _orders.Add(_order);
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        _valid = new PushSubscriptionInput(Endpoint, Base64Url.EncodeToString([0x04, .. q.X!, .. q.Y!]),
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)));
    }

    private SubscribeToPush Subscribe => new(_orders, _unitOfWork, new FakeTimeProvider(Now));

    [Fact]
    public async Task Subscription_is_saved_on_the_work_order_found_by_its_code()
    {
        var result = await Subscribe.HandleAsync("7k3m-9qx2-hfda", _valid, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(Endpoint, Assert.Single(_order.PushSubscriptions).Endpoint);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("kısa")]
    [InlineData("7K3M9QX2HFDB")]
    public async Task Unknown_code_finds_nothing(string? code)
    {
        Assert.Equal(ApplicationErrors.WorkOrderNotFound, (await Subscribe.HandleAsync(code!, _valid, default)).Error);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Invalid_subscription_is_not_saved()
    {
        var result = await Subscribe.HandleAsync(Code, _valid with { Endpoint = "https://169.254.169.254/" }, default);

        Assert.Equal(WorkOrderErrors.InvalidPushSubscription, result.Error);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Unsubscribing_removes_the_browser()
    {
        await Subscribe.HandleAsync(Code, _valid, default);

        var result = await new UnsubscribeFromPush(_orders, _unitOfWork).HandleAsync(Code, Endpoint, default);

        Assert.True(result.IsSuccess);
        Assert.Empty(_order.PushSubscriptions);
    }
}
