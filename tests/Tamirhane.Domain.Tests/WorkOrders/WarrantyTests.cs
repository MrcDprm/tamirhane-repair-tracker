using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class WarrantyTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static WorkOrder Diagnosing()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Şarj olmuyor", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);
        return order;
    }

    private static WorkOrder Repaired(int warrantyDays = 90)
    {
        var order = Diagnosing();
        order.StartRepair(Staff, Now);
        order.MarkReady(Staff, Now, new BranchStock(order.BranchId, []));
        order.Deliver(Staff, Now, warrantyDays);
        return order;
    }

    private static Domain.Common.Result<WorkOrder> ReturnUnderWarranty(WorkOrder previous, DateTimeOffset at) =>
        WorkOrder.ReceiveUnderWarranty(previous, previous.BranchId, "2026-0002", "ABCDEFGH2346",
            "Aynı sorun tekrar etti", null, null, null, Staff, at);

    [Fact]
    public void Warranty_starts_at_delivery()
    {
        var order = Repaired(90);

        Assert.Equal(Now.AddDays(90), order.WarrantyUntil);
        Assert.True(order.IsUnderWarranty(Now.AddDays(90)));
        Assert.False(order.IsUnderWarranty(Now.AddDays(90).AddSeconds(1)));
    }

    [Fact]
    public void Zero_days_means_no_warranty()
    {
        var order = Repaired(0);

        Assert.Null(order.WarrantyUntil);
        Assert.False(order.IsUnderWarranty(Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(WorkOrder.MaxWarrantyDays + 1)]
    public void Warranty_days_are_validated(int days)
    {
        var order = Diagnosing();
        order.StartRepair(Staff, Now);
        order.MarkReady(Staff, Now, new BranchStock(order.BranchId, []));

        Assert.Equal(WorkOrderErrors.InvalidWarrantyDays, order.Deliver(Staff, Now, days).Error);
        Assert.Equal(WorkOrderStatus.Ready, order.Status);
    }

    [Fact]
    public void Device_returned_unrepaired_gets_no_warranty()
    {
        var order = Diagnosing();
        order.AddQuoteLine(QuoteLineKind.Labor, null, "Anakart onarımı", 1, 3000m, 20, 0);
        order.SendQuote(Staff, Now);
        order.DecideQuote(false, "Ayşe Kaya", null, Now);

        order.Deliver(Staff, Now, 90);

        Assert.Equal(WorkOrderStatus.Delivered, order.Status);
        Assert.Null(order.WarrantyUntil);
    }

    [Fact]
    public void Warranty_return_links_to_the_previous_order_and_keeps_customer_and_device()
    {
        var previous = Repaired();

        var order = ReturnUnderWarranty(previous, Now.AddDays(30)).Value;

        Assert.True(order.IsWarrantyReturn);
        Assert.Equal(previous.Id, order.WarrantyOfId);
        Assert.Equal(previous.CustomerId, order.CustomerId);
        Assert.Equal(previous.DeviceId, order.DeviceId);
        Assert.Equal(previous.ShopId, order.ShopId);
        Assert.Equal(WorkOrderStatus.Received, order.Status);
    }

    [Fact]
    public void Expired_warranty_cannot_be_used()
    {
        var previous = Repaired(90);

        Assert.Equal(WorkOrderErrors.NotUnderWarranty, ReturnUnderWarranty(previous, Now.AddDays(91)).Error);
    }

    [Fact]
    public void Undelivered_order_cannot_be_reopened_under_warranty()
    {
        var previous = Diagnosing();

        Assert.Equal(WorkOrderErrors.NotUnderWarranty, ReturnUnderWarranty(previous, Now).Error);
    }

    [Fact]
    public void Warranty_return_still_validates_the_complaint()
    {
        var previous = Repaired();

        var result = WorkOrder.ReceiveUnderWarranty(previous, previous.BranchId, "2026-0002", "ABCDEFGH2346",
            " ", null, null, null, Staff, Now);

        Assert.Equal(WorkOrderErrors.ComplaintRequired, result.Error);
    }
}
