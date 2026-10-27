using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>Panel sorguları gerçek PostgreSQL'de: durum sayıları, 30 günü geçenler, aylık ciro, başka atölye sayılmaz.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardQueriesTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(3));
    private static readonly Guid Staff = Guid.NewGuid();

    [DockerFact]
    public async Task Open_work_is_counted_by_status_and_age()
    {
        var shopId = Guid.NewGuid();
        await SeedAsync(shopId, Now, o => { });                                                // teslim alındı
        await SeedAsync(shopId, Now, o => o.StartDiagnosis(Staff, Now));                         // inceleniyor
        await SeedAsync(shopId, Now, AwaitingApproval);                                          // onay bekliyor
        await SeedAsync(shopId, Now, o => { o.StartDiagnosis(Staff, Now); o.StartRepair(Staff, Now); });
        await SeedAsync(shopId, Now, Ready);
        await SeedAsync(shopId, Now, o => { AwaitingApproval(o); o.DecideQuote(false, "Ayşe Kaya", null, Now); }); // reddedildi: teslime hazır
        await SeedAsync(shopId, Now.AddDays(-40), o => { });                                     // 40 gündür bekliyor
        await SeedAsync(shopId, Now.AddDays(-40), o => { Ready(o); o.Deliver(Staff, Now, 90); }); // kapanmış: sayılmaz
        await SeedAsync(Guid.NewGuid(), Now, AwaitingApproval);                                  // başka atölye

        using var scope = db.AsShop(shopId);
        var counts = await scope.Get<IDashboardQueries>().CountsAsync(Now.AddDays(-30), default);

        Assert.Equal(new WorkOrderCounts(Open: 7, AwaitingApproval: 1, InRepair: 1, ReadyForPickup: 2, Overdue: 1), counts);
    }

    [DockerFact]
    public async Task Revenue_is_payments_minus_refunds_within_the_month_of_this_shop_only()
    {
        var shopId = Guid.NewGuid();
        var monthStart = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(3));
        await SeedAsync(shopId, Now, o =>
        {
            Quoted(o);
            o.RecordPayment(400, PaymentMethod.Cash, Staff, monthStart, null);                 // ayın ilk anı: dahil
            o.RecordPayment(300, PaymentMethod.Card, Staff, Now, null);
            o.RecordRefund(100, PaymentMethod.Cash, Staff, Now, null);                         // iade düşülür
            o.RecordPayment(50, PaymentMethod.Cash, Staff, monthStart.AddSeconds(-1), null);   // geçen ay
        });
        await SeedAsync(Guid.NewGuid(), Now, o => { Quoted(o); o.RecordPayment(999, PaymentMethod.Cash, Staff, Now, null); });

        using var scope = db.AsShop(shopId);
        var queries = scope.Get<IDashboardQueries>();

        Assert.Equal(600m, await queries.RevenueAsync(monthStart, monthStart.AddMonths(1), default));
        Assert.Equal(50m, await queries.RevenueAsync(monthStart.AddMonths(-1), monthStart, default));
    }

    [DockerFact]
    public async Task An_empty_shop_has_zero_everywhere()
    {
        using var scope = db.AsShop(Guid.NewGuid());
        var queries = scope.Get<IDashboardQueries>();

        Assert.Equal(new WorkOrderCounts(0, 0, 0, 0, 0), await queries.CountsAsync(Now.AddDays(-30), default));
        Assert.Equal(0m, await queries.RevenueAsync(Now.AddMonths(-1), Now, default));
    }

    private static void Quoted(WorkOrder o)
    {
        o.StartDiagnosis(Staff, Now);
        o.AddQuoteLine(QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000, 20, 0);
    }

    private static void AwaitingApproval(WorkOrder o)
    {
        Quoted(o);
        o.SendQuote(Staff, Now);
    }

    private static void Ready(WorkOrder o)
    {
        o.StartDiagnosis(Staff, Now);
        o.StartRepair(Staff, Now);
        o.MarkReady(Staff, Now, new BranchStock(o.BranchId, []));
    }

    private async Task SeedAsync(Guid shopId, DateTimeOffset createdAt, Action<WorkOrder> steps)
    {
        using var shop = db.AsShop(shopId);
        var phone = $"0532 {Random.Shared.Next(100, 999)} {Random.Shared.Next(10, 99)} {Random.Shared.Next(10, 99)}";
        var customer = Customer.Create(shopId, "Ayşe Kaya", phone, null, true, createdAt).Value;
        var device = Device.Create(customer, DeviceKind.Phone, "Apple", "iPhone 13", null, null).Value;
        var order = WorkOrder.Receive(shopId, Guid.NewGuid(), $"2026-{Random.Shared.Next(1000, 9999)}", TrackingCode.Generate(),
            customer.Id, device.Id, "Ekran kırık", null, null, null, Staff, createdAt).Value;
        steps(order);
        shop.Customers.Add(customer);
        shop.Db.Devices.Add(device);
        shop.Orders.Add(order);
        await shop.UnitOfWork.SaveChangesAsync(default);
    }
}
