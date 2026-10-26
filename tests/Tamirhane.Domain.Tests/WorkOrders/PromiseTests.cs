using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

/// <summary>Müşteriye söz verilen teslim günü.</summary>
public class PromiseTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.DateTime);

    private static WorkOrder NewOrder() =>
        WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;

    [Fact]
    public void Promise_can_be_given_changed_and_removed()
    {
        var order = NewOrder();
        Assert.Null(order.PromisedDate);

        Assert.True(order.Promise(Today.AddDays(3), Today).IsSuccess);
        Assert.Equal(Today.AddDays(3), order.PromisedDate);

        Assert.True(order.Promise(Today, Today).IsSuccess); // bugün de olur
        Assert.True(order.Promise(null, Today).IsSuccess);
        Assert.Null(order.PromisedDate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(WorkOrder.MaxPromiseDays + 1)]
    public void Past_or_too_far_days_are_rejected(int days)
    {
        var order = NewOrder();
        order.Promise(Today.AddDays(2), Today);

        Assert.Equal(WorkOrderErrors.InvalidPromisedDate, order.Promise(Today.AddDays(days), Today).Error);
        Assert.Equal(Today.AddDays(2), order.PromisedDate);
    }

    [Fact]
    public void Closed_work_order_keeps_its_promise()
    {
        var order = NewOrder();
        order.Cancel(Staff, Now, "Müşteri vazgeçti", new BranchStock(order.BranchId, []));

        Assert.Equal(WorkOrderErrors.Closed, order.Promise(Today.AddDays(1), Today).Error);
    }
}
