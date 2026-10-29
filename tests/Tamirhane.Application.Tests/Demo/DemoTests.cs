using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Demo;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.Demo;

/// <summary>Demo atölye: örnek veri tutarlı ve her ekranı dolduruyor; açılış sınırı ve şifresiz oturum sadece demo hesabına.</summary>
public class DemoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 14, 0, 0, TimeSpan.FromHours(3));

    private static DemoWorkshop Build(string locale = "tr") => DemoData.Build(locale, Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void Sample_shop_is_a_demo_with_an_owner_and_a_technician()
    {
        var demo = Build();

        Assert.True(demo.Shop.IsDemo);
        Assert.Equal([StaffRole.Owner, StaffRole.Technician], demo.Staff.Select(s => s.Role));
        Assert.All(demo.Staff, s => Assert.Equal(demo.Branch.Id, s.BranchId));
    }

    [Fact]
    public void Every_status_and_list_filter_has_a_sample()
    {
        var orders = Build().WorkOrders;

        foreach (var status in Enum.GetValues<WorkOrderStatus>())
            Assert.Contains(orders, o => o.Status == status);
        Assert.Contains(orders, o => !o.IsClosed && o.CreatedAt < Now.AddDays(-30)); // 30 günü geçen
        Assert.Contains(orders, o => o.Status == WorkOrderStatus.Ready && o.Balance > 0); // borçlu hazır
        Assert.Contains(orders, o => o.IsUnderWarranty(Now)); // garanti dönüşü denenebilir
        Assert.Contains(orders, o => o.Status == WorkOrderStatus.Diagnosing && o.QuoteLines.Count > 0); // taslak teklif
    }

    [Fact]
    public void Numbers_follow_the_dates_and_the_counter_continues_after_them()
    {
        var demo = Build();

        var numbers = demo.WorkOrders.OrderBy(o => o.CreatedAt).Select(o => o.Number).ToList();
        Assert.Equal(numbers.Order(StringComparer.Ordinal), numbers);
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal(demo.WorkOrders.Count, demo.Sequences.Values.Sum());
        Assert.All(demo.WorkOrders, o => Assert.Equal(demo.Shop.Id, o.ShopId));
    }

    [Fact]
    public void History_is_in_order_and_never_in_the_future()
    {
        foreach (var order in Build().WorkOrders)
        {
            var times = order.History.Select(h => h.At).ToList();
            Assert.Equal(times.Order(), times);
            Assert.All(times, t => Assert.True(t <= Now, $"{order.Number}: {t}"));
        }
    }

    [Fact]
    public void Delivered_work_is_paid_and_stock_used_in_repairs_is_counted()
    {
        var demo = Build();

        Assert.All(demo.WorkOrders.Where(o => o.Status == WorkOrderStatus.Delivered), o => Assert.Equal(0, o.Balance));
        var used = demo.StockMovements.Where(m => m.Reason == Tamirhane.Domain.Inventory.StockMovementReason.Used).ToList();
        Assert.NotEmpty(used);
        // Her parçanın stoğu, hareketlerinin toplamı; en az iki parça kritik seviyenin altında (uyarı görünsün).
        foreach (var level in demo.StockLevels)
            Assert.Equal(demo.StockMovements.Where(m => m.PartId == level.PartId).Sum(m => m.Change), level.Quantity);
        Assert.True(demo.Parts.Count(p => demo.StockLevels.Single(l => l.PartId == p.Id).Quantity < p.CriticalLevel) >= 2);
    }

    [Fact]
    public void English_demo_is_written_in_english()
    {
        var demo = Build("en");

        Assert.Equal(("Sample Repair Shop", "en"), (demo.Shop.Name, demo.Shop.Locale));
        Assert.Contains(demo.WorkOrders, o => o.Complaint == "Face ID doesn't work");
    }

    [Fact]
    public void Demo_accounts_use_an_address_that_never_receives_email()
    {
        Assert.EndsWith("@demo.tamirhane.invalid", DemoData.Email("owner"));
        Assert.NotEqual(DemoData.Email("owner"), DemoData.Email("owner"));
        Assert.NotEqual(DemoData.Password(), DemoData.Password());
    }

    [Fact]
    public async Task Start_opens_the_demo_and_signs_in_as_its_owner()
    {
        var (start, demos, accounts, unitOfWork) = Start();

        Assert.True((await start.HandleAsync("en", default)).IsSuccess);

        var demo = Assert.Single(demos.Added);
        Assert.Equal("en", demo.Shop.Locale);
        Assert.Equal(2, accounts.Confirmed.Count);
        Assert.All(accounts.Users.Keys, e => Assert.EndsWith("@" + DemoData.EmailDomain, e));
        var owner = demo.Staff.Single(s => s.Role == StaffRole.Owner);
        Assert.Equal([owner.UserId!.Value], accounts.DemoSignIns);
        Assert.Equal((1, 1), (unitOfWork.Saves, unitOfWork.Commits));
    }

    [Fact]
    public async Task Unknown_language_falls_back_to_turkish()
    {
        var (start, demos, _, _) = Start();

        await start.HandleAsync("de", default);

        Assert.Equal("tr", Assert.Single(demos.Added).Shop.Locale);
    }

    [Fact]
    public async Task No_new_demo_when_too_many_are_open()
    {
        var (start, demos, accounts, _) = Start();
        demos.Open = StartDemo.MaxOpenDemos;

        Assert.Equal(ApplicationErrors.DemoFull, (await start.HandleAsync("tr", default)).Error);
        Assert.Empty(demos.Added);
        Assert.Empty(accounts.Users);
        Assert.Empty(accounts.DemoSignIns);
    }

    [Fact]
    public async Task Only_demos_of_the_last_day_are_counted()
    {
        var (start, demos, _, _) = Start();

        await start.HandleAsync("tr", default);

        Assert.Equal(Now - Shop.DemoLifetime, demos.CountedSince);
    }

    private static (StartDemo, FakeDemoShops, FakeAccounts, FakeUnitOfWork) Start()
    {
        var demos = new FakeDemoShops();
        var accounts = new FakeAccounts();
        var unitOfWork = new FakeUnitOfWork();
        return (new StartDemo(demos, accounts, unitOfWork, new FakeTimeProvider(Now)), demos, accounts, unitOfWork);
    }

    private sealed class FakeDemoShops : IDemoShops
    {
        public int Open { get; set; }
        public DateTimeOffset? CountedSince { get; private set; }
        public List<DemoWorkshop> Added { get; } = [];

        public Task<int> CountOpenedSinceAsync(DateTimeOffset since, CancellationToken ct)
        {
            CountedSince = since;
            return Task.FromResult(Open);
        }

        public void Add(DemoWorkshop workshop) => Added.Add(workshop);
    }
}
