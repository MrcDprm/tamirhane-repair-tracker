using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>Bir atölye başka atölyenin kaydını ne okuyabilir ne de ona yazabilir.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantFilterTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(3));

    private readonly Guid _shopA = Guid.NewGuid();
    private readonly Guid _shopB = Guid.NewGuid();

    [DockerFact]
    public async Task Shop_cannot_open_another_shops_work_order_by_id()
    {
        var order = await ReceiveAsync(_shopB, "0532 111 11 11");

        using var a = db.AsShop(_shopA);
        Assert.Null(await a.Orders.GetAsync(order.Id, default));

        // Kullanım senaryosu da "bulunamadı" der (web katmanında 404); kaydın varlığı sızmaz.
        var result = await new StartDiagnosis(a.Orders, a.UnitOfWork, new NullQueue(), a.User, TimeProvider.System)
            .HandleAsync(order.Id, default);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound, result.Error);

        using var b = db.AsShop(_shopB);
        Assert.NotNull(await b.Orders.GetAsync(order.Id, default));
    }

    [DockerFact]
    public async Task Shop_does_not_find_another_shops_customer_by_phone()
    {
        await ReceiveAsync(_shopB, "0532 222 22 22");
        var phone = PhoneNumber.Parse("0532 222 22 22").Value;

        using var a = db.AsShop(_shopA);
        Assert.Null(await a.Customers.FindByPhoneAsync(phone, default));
        Assert.Empty(await a.Db.Devices.ToListAsync());
    }

    [DockerFact]
    public async Task Tracking_code_finds_work_order_without_a_session()
    {
        var order = await ReceiveAsync(_shopB, "0532 333 33 33");

        using var anonymous = db.AsShop(Guid.Empty);
        Assert.Empty(await anonymous.Db.WorkOrders.ToListAsync());
        Assert.NotNull(await anonymous.Orders.GetByTrackingCodeAsync(order.TrackingCode, default));
    }

    [DockerFact]
    public async Task Stock_gets_shop_from_session_and_stays_hidden_from_other_shops()
    {
        var branchId = Guid.NewGuid();
        Guid partId;
        using (var b = db.AsShop(_shopB))
        {
            var part = Part.Create(_shopB, "LCD-" + Guid.NewGuid().ToString("N")[..8], "iPhone 13 ekran", 1500, 2500, 2).Value;
            var level = StockLevel.Create(part.Id, branchId);
            var movement = level.Receive(5, b.User.StaffId, Now, null).Value;
            b.Parts.Add(part);
            b.Stock.AddLevel(level);
            b.Stock.AddMovements([movement]);
            await b.UnitOfWork.SaveChangesAsync(default);
            partId = part.Id;

            var saved = await b.Db.StockLevels.SingleAsync(l => l.PartId == partId);
            Assert.Equal(_shopB, b.Db.Entry(saved).Property<Guid>("ShopId").CurrentValue);
        }

        using var a = db.AsShop(_shopA);
        Assert.Null(await a.Parts.GetAsync(partId, default));
        Assert.Null(await a.Stock.GetLevelAsync(partId, branchId, default));
        Assert.Empty(await a.Db.StockMovements.Where(m => m.PartId == partId).ToListAsync());
    }

    [DockerFact]
    public async Task Writing_a_record_for_another_shop_is_refused()
    {
        using var a = db.AsShop(_shopA);
        a.Customers.Add(Customer.Create(_shopB, "Ayşe Kaya", "0532 444 44 44", null, true, Now).Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() => a.UnitOfWork.SaveChangesAsync(default));
    }

    /// <summary>Verilen atölyeye müşteri, cihaz ve iş emri kaydeder.</summary>
    private async Task<WorkOrder> ReceiveAsync(Guid shopId, string phone)
    {
        using var shop = db.AsShop(shopId);
        var customer = Customer.Create(shopId, "Ayşe Kaya", phone, null, true, Now).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        var sequence = await shop.Orders.NextSequenceAsync(shopId, Now.Year, default);
        var order = WorkOrder.Receive(shopId, Guid.NewGuid(), WorkOrderNumber.Format(Now.Year, sequence), TrackingCode.Generate(),
            customer.Id, device.Id, "Ekran kırık", null, null, null, shop.User.StaffId, Now).Value;

        shop.Customers.Add(customer);
        shop.Db.Devices.Add(device);
        shop.Orders.Add(order);
        await shop.UnitOfWork.SaveChangesAsync(default);
        return order;
    }

    private sealed class NullQueue : INotificationQueue
    {
        public void Enqueue(WorkOrderNotification notification) { }
    }
}
