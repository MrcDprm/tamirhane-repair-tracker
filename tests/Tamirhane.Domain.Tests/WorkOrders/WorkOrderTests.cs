using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class WorkOrderTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static WorkOrder NewOrder(string complaint = "Ekran kırık", string? passcode = "1234") =>
        WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), complaint, "Kasada çizik", "Kılıf", passcode, Staff, Start).Value;

    private static BranchStock NoStock(WorkOrder order) => new(order.BranchId, []);

    private static WorkOrder InRepair()
    {
        var order = NewOrder();
        order.StartDiagnosis(Staff, Start.AddHours(1));
        order.StartRepair(Staff, Start.AddHours(2));
        return order;
    }

    [Fact]
    public void Receive_starts_in_received_with_one_history_entry()
    {
        var order = NewOrder();

        Assert.Equal(WorkOrderStatus.Received, order.Status);
        var entry = Assert.Single(order.History);
        Assert.Null(entry.From);
        Assert.Equal(WorkOrderStatus.Received, entry.To);
        Assert.Equal(Staff, entry.StaffId);
        Assert.Equal(Start, entry.At);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Receive_requires_a_complaint(string complaint)
    {
        var result = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), complaint, null, null, null, Staff, Start);

        Assert.Equal(WorkOrderErrors.ComplaintRequired, result.Error);
    }

    [Fact]
    public void Receive_rejects_too_long_text()
    {
        var result = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), new string('a', WorkOrder.MaxTextLength + 1), null, null, null, Staff, Start);

        Assert.Equal(WorkOrderErrors.TextTooLong, result.Error);
    }

    [Fact]
    public void Receive_trims_text_and_drops_blank_optional_fields()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "  Şarj olmuyor  ", " ", "", null, Staff, Start).Value;

        Assert.Equal("Şarj olmuyor", order.Complaint);
        Assert.Null(order.Condition);
        Assert.Null(order.Accessories);
    }

    [Fact]
    public void Happy_path_without_quote_reaches_delivered()
    {
        var order = InRepair();

        Assert.True(order.MarkReady(Staff, Start.AddDays(1), NoStock(order)).IsSuccess);
        Assert.True(order.Deliver(Staff, Start.AddDays(2)).IsSuccess);

        Assert.Equal(WorkOrderStatus.Delivered, order.Status);
        Assert.Equal(Start.AddDays(2), order.DeliveredAt);
        Assert.Equal(
            [WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Delivered],
            order.History.Select(h => h.To));
    }

    [Fact]
    public void Each_transition_records_where_it_came_from()
    {
        var order = InRepair();

        var last = order.History[^1];
        Assert.Equal(WorkOrderStatus.Diagnosing, last.From);
        Assert.Equal(WorkOrderStatus.InRepair, last.To);
        Assert.Equal(Start.AddHours(2), last.At);
    }

    [Fact]
    public void Deliver_removes_the_device_passcode()
    {
        var order = InRepair();
        order.MarkReady(Staff, Start.AddDays(1), NoStock(order));

        order.Deliver(Staff, Start.AddDays(2));

        Assert.Null(order.Passcode);
    }

    [Fact]
    public void Invalid_transition_fails_and_changes_nothing()
    {
        var order = NewOrder();

        var result = order.MarkReady(Staff, Start.AddHours(1), NoStock(order));

        Assert.Equal(WorkOrderErrors.InvalidTransition, result.Error);
        Assert.Equal(WorkOrderStatus.Received, order.Status);
        Assert.Single(order.History);
    }

    [Fact]
    public void Cannot_start_repair_before_diagnosis()
    {
        var order = NewOrder();

        Assert.Equal(WorkOrderErrors.InvalidTransition, order.StartRepair(Staff, Start).Error);
    }

    [Fact]
    public void Cannot_deliver_before_ready()
    {
        var order = InRepair();

        Assert.Equal(WorkOrderErrors.InvalidTransition, order.Deliver(Staff, Start.AddDays(1)).Error);
        Assert.Null(order.DeliveredAt);
    }

    [Fact]
    public void Cancel_requires_a_reason_and_keeps_it_in_history()
    {
        var order = NewOrder();

        Assert.Equal(WorkOrderErrors.CancelReasonRequired, order.Cancel(Staff, Start, " ", NoStock(order)).Error);
        Assert.True(order.Cancel(Staff, Start.AddHours(1), "Müşteri vazgeçti", NoStock(order)).IsSuccess);

        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
        Assert.Equal("Müşteri vazgeçti", order.History[^1].Note);
        Assert.Null(order.Passcode);
    }

    [Fact]
    public void Closed_orders_accept_no_further_transitions()
    {
        var cancelled = NewOrder();
        cancelled.Cancel(Staff, Start, "Müşteri vazgeçti", NoStock(cancelled));

        var delivered = InRepair();
        delivered.MarkReady(Staff, Start.AddDays(1), NoStock(delivered));
        delivered.Deliver(Staff, Start.AddDays(2));

        foreach (var order in new[] { cancelled, delivered })
        {
            Assert.True(order.IsClosed);
            Assert.Equal(WorkOrderErrors.InvalidTransition, order.StartDiagnosis(Staff, Start).Error);
            Assert.Equal(WorkOrderErrors.InvalidTransition, order.Cancel(Staff, Start, "Tekrar", NoStock(order)).Error);
        }
    }

    [Theory]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, true)]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.InRepair, false)]
    [InlineData(WorkOrderStatus.Received, WorkOrderStatus.Cancelled, true)]
    public void CanMoveTo_follows_the_transition_table(WorkOrderStatus from, WorkOrderStatus to, bool expected)
    {
        var order = NewOrder();
        Assert.Equal(from, order.Status);

        Assert.Equal(expected, order.CanMoveTo(to));
    }
}
