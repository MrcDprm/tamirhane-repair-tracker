using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

public class ReceiveDeviceTests
{
    [Fact]
    public async Task New_customer_device_and_work_order_are_saved_together()
    {
        var shop = new TestShop();

        var received = (await shop.ReceiveDevice().HandleAsync(TestShop.NewCustomerCommand(), default)).Value;

        var order = Assert.Single(shop.Orders.Items);
        var customer = Assert.Single(shop.Customers.Items);
        var device = Assert.Single(shop.Devices.Items);
        Assert.Equal(order.Id, received.WorkOrderId);
        Assert.Equal("2026-0001", received.Number);
        Assert.Equal(TrackingCode.Length, received.TrackingCode.Length);
        Assert.Equal(shop.User.ShopId, order.ShopId);
        Assert.Equal(shop.User.BranchId, order.BranchId);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(device.Id, order.DeviceId);
        Assert.Equal(customer.Id, device.CustomerId);
        Assert.Equal(1, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Customer_is_notified_that_the_device_was_received()
    {
        var shop = new TestShop();

        var id = await shop.ReceiveAsync();

        var notification = Assert.Single(shop.Queue.Sent);
        Assert.Equal(new WorkOrderNotification(shop.User.ShopId, id, WorkOrderStatus.Received), notification);
    }

    [Fact]
    public async Task Numbers_continue_within_the_year()
    {
        var shop = new TestShop();

        await shop.ReceiveAsync("0532 000 00 01");
        await shop.ReceiveAsync("0532 000 00 02");

        Assert.Equal(["2026-0001", "2026-0002"], shop.Orders.Items.Select(o => o.Number));
    }

    [Fact]
    public async Task Year_follows_turkish_time_not_server_time()
    {
        var shop = new TestShop();
        // Sunucu (UTC) için hâlâ 31 Aralık 21:30; Türkiye'de 1 Ocak 00:30.
        shop.Clock.SetUtcNow(new DateTimeOffset(2026, 12, 31, 21, 30, 0, TimeSpan.Zero));

        var received = (await shop.ReceiveDevice().HandleAsync(TestShop.NewCustomerCommand(), default)).Value;

        Assert.StartsWith("2027-", received.Number);
    }

    [Fact]
    public async Task Second_customer_with_the_same_phone_is_refused()
    {
        var shop = new TestShop();
        await shop.ReceiveAsync("0532 123 45 67");

        var result = await shop.ReceiveDevice().HandleAsync(TestShop.NewCustomerCommand("+90 532 123 4567"), default);

        Assert.Equal(ApplicationErrors.DuplicatePhone, result.Error);
        Assert.Single(shop.Customers.Items);
        Assert.Equal(1, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Existing_customer_and_device_are_reused()
    {
        var shop = new TestShop();
        await shop.ReceiveAsync();
        var customer = shop.Customers.Items[0];
        var device = shop.Devices.Items[0];

        var command = TestShop.NewCustomerCommand() with { CustomerId = customer.Id, DeviceId = device.Id, CustomerPhone = null };
        var result = await shop.ReceiveDevice().HandleAsync(command, default);

        Assert.True(result.IsSuccess);
        Assert.Single(shop.Customers.Items);
        Assert.Single(shop.Devices.Items);
        Assert.Equal(device.Id, shop.Orders.Items[1].DeviceId);
    }

    [Fact]
    public async Task Device_of_another_customer_cannot_be_chosen()
    {
        var shop = new TestShop();
        await shop.ReceiveAsync("0532 000 00 01");
        await shop.ReceiveAsync("0532 000 00 02");
        var first = shop.Customers.Items[0];
        var othersDevice = shop.Devices.Items[1];

        var command = TestShop.NewCustomerCommand() with { CustomerId = first.Id, DeviceId = othersDevice.Id };
        var result = await shop.ReceiveDevice().HandleAsync(command, default);

        Assert.Equal(ApplicationErrors.DeviceNotFound, result.Error);
    }

    [Fact]
    public async Task Customer_of_another_shop_is_not_found()
    {
        var shop = new TestShop();
        var other = Customer.Create(Guid.NewGuid(), "Başka Atölye Müşterisi", "0555 555 55 55", null, true, DateTimeOffset.UtcNow).Value;
        shop.Customers.Items.Add(other);

        var command = TestShop.NewCustomerCommand() with { CustomerId = other.Id };
        var result = await shop.ReceiveDevice().HandleAsync(command, default);

        Assert.Equal(ApplicationErrors.CustomerNotFound, result.Error);
    }

    [Fact]
    public async Task Invalid_input_saves_nothing_and_sends_nothing()
    {
        var shop = new TestShop();

        var result = await shop.ReceiveDevice().HandleAsync(TestShop.NewCustomerCommand(complaint: " "), default);

        Assert.Equal(WorkOrderErrors.ComplaintRequired, result.Error);
        Assert.Equal(0, shop.UnitOfWork.Saves);
        Assert.Empty(shop.Queue.Sent);
    }

    [Fact]
    public async Task Customer_lookup_finds_any_spelling_of_the_phone()
    {
        var shop = new TestShop();
        await shop.ReceiveAsync("0532 123 45 67");

        var found = (await shop.FindCustomerByPhone().HandleAsync("+90 (532) 123-4567", default)).Value;
        var missing = (await shop.FindCustomerByPhone().HandleAsync("0532 999 99 99", default)).Value;
        var invalid = await shop.FindCustomerByPhone().HandleAsync("12", default);

        Assert.NotNull(found);
        Assert.Equal("Ayşe Kaya", found.Name);
        Assert.Equal("iPhone 13", Assert.Single(found.Devices).Model);
        Assert.Null(missing);
        Assert.Equal(CustomerErrors.InvalidPhone, invalid.Error);
    }

    [Fact]
    public async Task Work_order_page_shows_customer_and_device_but_not_the_passcode()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();

        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

        Assert.Equal(("2026-0001", WorkOrderStatus.Received, "Ekran kırık"), (view.Number, view.Status, view.Complaint));
        Assert.Equal(("Ayşe Kaya", "+905321234567"), (view.CustomerName, view.CustomerPhone));
        Assert.Equal((DeviceKind.Phone, "Apple", "iPhone 13", "490154203237518"), (view.DeviceKind, view.Brand, view.Model, view.Imei));
        Assert.True(view.HasPasscode);
        Assert.DoesNotContain(typeof(WorkOrderView).GetProperties(), p => p.Name == "Passcode"); // şifrenin kendisi sayfaya gitmez
    }

    [Fact]
    public async Task Another_shops_work_order_is_not_found()
    {
        var other = new TestShop();
        var foreign = await other.ReceiveAsync();
        var shop = new TestShop();
        shop.Orders.Items.AddRange(other.Orders.Items); // aynı depoda olsa bile kiracı filtresi gizler

        Assert.Equal(ApplicationErrors.WorkOrderNotFound, (await shop.GetWorkOrder().HandleAsync(foreign, default)).Error);
    }
}
