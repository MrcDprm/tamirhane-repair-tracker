using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>Takip sayfası: koddan bulunur, isim maskeli, yol geçilen ve gelecek duraklardan oluşur, atölye içi not yok.</summary>
public class TrackingViewTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(3));
    private const string Code = "7K3M9QX2HFDA";

    private readonly FakeWorkOrders _orders = new(new FakeUser(Guid.NewGuid()));
    private readonly WorkOrder _order;
    private readonly GetTracking _get;

    public TrackingViewTests()
    {
        _order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0042", Code, Guid.NewGuid(), Guid.NewGuid(),
            "Ekran kırık", null, null, "4826", Staff, Now).Value;
        _orders.Add(_order);
        _get = new GetTracking(_orders, new Directory());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kısa")]
    [InlineData("7K3M9QX2HFDB")] // geçerli biçim, kayıt yok
    public async Task Invalid_or_unknown_code_finds_nothing(string? code)
    {
        Assert.Null(await _get.HandleAsync(code, default));
    }

    [Fact]
    public async Task Code_is_read_however_the_customer_writes_it()
    {
        Assert.NotNull(await _get.HandleAsync("7k3m-9qx2-hfda", default));
    }

    [Fact]
    public async Task Customer_name_is_masked_and_shop_contact_is_shown()
    {
        var view = (await _get.HandleAsync(Code, default))!;

        Assert.Equal("Ay** Ka**", view.MaskedCustomerName);
        Assert.Equal(("Kaya Telefon", "Merkez", "Moda Cd. No: 12", "+902163456789"),
            (view.ShopName, view.BranchName, view.BranchAddress, view.BranchPhone));
        Assert.Equal(("Apple", "iPhone 13", "Ekran kırık"), (view.Brand, view.Model, view.Complaint));
    }

    [Fact]
    public async Task Road_shows_passed_steps_with_time_and_the_rest_ahead()
    {
        _order.StartDiagnosis(Staff, Now.AddHours(1));

        var steps = (await _get.HandleAsync(Code, default))!.Steps;

        Assert.Equal(
            [WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
            steps.Select(s => s.Status));
        Assert.Equal([Now, Now.AddHours(1), null, null, null], steps.Select(s => s.At));
    }

    [Fact]
    public async Task Cancelled_order_road_ends_where_it_stopped()
    {
        _order.Cancel(Staff, Now.AddHours(2), "Müşteri vazgeçti", new Domain.Inventory.BranchStock(_order.BranchId, []));

        var view = (await _get.HandleAsync(Code, default))!;

        Assert.True(view.IsClosed);
        Assert.Equal([WorkOrderStatus.Received, WorkOrderStatus.Cancelled], view.Steps.Select(s => s.Status));
        Assert.All(view.Steps, s => Assert.NotNull(s.At));
    }

    [Fact]
    public async Task Only_notes_meant_for_the_customer_are_shown()
    {
        _order.AddNote("Parça siparişi verildi, perşembe gelecek", visibleToCustomer: true, Staff, Now);
        _order.AddNote("Anakartta sıvı izi var, müşteri inkâr ediyor", visibleToCustomer: false, Staff, Now);

        var notes = (await _get.HandleAsync(Code, default))!.Notes;

        Assert.Equal(["Parça siparişi verildi, perşembe gelecek"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task Promised_date_reaches_the_page()
    {
        _order.Promise(new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 26));

        Assert.Equal(new DateOnly(2026, 10, 30), (await _get.HandleAsync(Code, default))!.PromisedDate);
    }

    private sealed class Directory : ITrackingDirectory
    {
        public Task<TrackingParties?> GetAsync(Guid shopId, Guid branchId, Guid customerId, Guid deviceId, CancellationToken ct) =>
            Task.FromResult<TrackingParties?>(new TrackingParties(
                "Kaya Telefon", "Merkez", "Moda Cd. No: 12", "+902163456789", "Ayşe Kaya", DeviceKind.Phone, "Apple", "iPhone 13"));
    }
}
