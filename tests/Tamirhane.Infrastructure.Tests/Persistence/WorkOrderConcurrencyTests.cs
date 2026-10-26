using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>
/// İş emrinin satır sürümü (xmin): aynı anda okunan iş emrini iki istek değiştirirse ikincisi kaydedilmez.
/// Müşteri teklif onayına çift tıkladığında iki karar yazılmasını bu engeller.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkOrderConcurrencyTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(3));

    [DockerFact]
    public async Task Second_of_two_simultaneous_decisions_is_refused()
    {
        var shopId = Guid.NewGuid();
        var code = await AwaitingApprovalAsync(shopId);

        // İki istek iş emrini aynı anda okur; ikisi de "onay bekliyor" görür.
        using var first = db.AsShop(Guid.Empty);
        using var second = db.AsShop(Guid.Empty);
        var a = (await first.Orders.GetByTrackingCodeAsync(code, default))!;
        var b = (await second.Orders.GetByTrackingCodeAsync(code, default))!;

        Assert.True(a.DecideQuote(true, "Ayşe Kaya", null, Now).IsSuccess);
        await first.UnitOfWork.SaveChangesAsync(default);

        Assert.True(b.DecideQuote(false, "Ayşe Kaya", null, Now).IsSuccess);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.UnitOfWork.SaveChangesAsync(default));

        using var check = db.AsShop(shopId);
        var saved = (await check.Orders.GetByTrackingCodeAsync(code, default))!;
        Assert.Equal(WorkOrderStatus.InRepair, saved.Status);
        Assert.Equal(QuoteDecisionKind.Approved, saved.QuoteDecision!.Kind);
        Assert.Single(saved.History, h => h.To is WorkOrderStatus.InRepair or WorkOrderStatus.Declined);
    }

    private async Task<string> AwaitingApprovalAsync(Guid shopId)
    {
        using var shop = db.AsShop(shopId);
        var customer = Customer.Create(shopId, "Ayşe Kaya", "0532 601 01 01", null, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        var order = WorkOrder.Receive(shopId, Guid.NewGuid(), "2026-0001", TrackingCode.Generate(),
            customer.Id, device.Id, "Ekran kırık", null, null, null, shop.User.StaffId, Now).Value;
        order.StartDiagnosis(shop.User.StaffId, Now);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000, 20, 0);
        order.SendQuote(shop.User.StaffId, Now);

        shop.Customers.Add(customer);
        shop.Db.Devices.Add(device);
        shop.Orders.Add(order);
        await shop.UnitOfWork.SaveChangesAsync(default);
        return order.TrackingCode;
    }
}
