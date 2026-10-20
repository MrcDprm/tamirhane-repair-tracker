using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class NoteAndIntakeTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static WorkOrder NewOrder() =>
        WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, "1234", Staff, Now).Value;

    [Fact]
    public void Notes_keep_their_visibility_author_and_time()
    {
        var order = NewOrder();

        order.AddNote(" Parça siparişi verildi ", visibleToCustomer: true, Staff, Now);
        order.AddNote("Anakartta sıvı izi var", visibleToCustomer: false, Staff, Now.AddHours(1));

        Assert.Equal(2, order.Notes.Count);
        Assert.Equal("Parça siparişi verildi", order.Notes[0].Text);
        Assert.True(order.Notes[0].VisibleToCustomer);
        Assert.False(order.Notes[1].VisibleToCustomer);
        Assert.Equal(Staff, order.Notes[1].StaffId);
        Assert.Equal(Now.AddHours(1), order.Notes[1].At);
    }

    [Fact]
    public void Empty_or_too_long_notes_are_rejected()
    {
        var order = NewOrder();

        Assert.Equal(WorkOrderErrors.NoteRequired, order.AddNote("  ", false, Staff, Now).Error);
        Assert.Equal(WorkOrderErrors.TextTooLong, order.AddNote(new string('a', 1001), false, Staff, Now).Error);
        Assert.Empty(order.Notes);
    }

    [Fact]
    public void Intake_details_can_be_corrected_while_open()
    {
        var order = NewOrder();

        Assert.True(order.UpdateIntake("Ekran kırık, dokunmatik çalışmıyor", "Arka cam çatlak", "Kılıf, şarj aleti", " ").IsSuccess);

        Assert.Equal("Ekran kırık, dokunmatik çalışmıyor", order.Complaint);
        Assert.Equal("Arka cam çatlak", order.Condition);
        Assert.Equal("Kılıf, şarj aleti", order.Accessories);
        Assert.Null(order.Passcode);
        Assert.Equal(WorkOrderErrors.ComplaintRequired, order.UpdateIntake(" ", null, null, null).Error);
    }

    [Fact]
    public void Closed_order_intake_cannot_change()
    {
        var order = NewOrder();
        order.Cancel(Staff, Now, "Müşteri vazgeçti", new BranchStock(order.BranchId, []));

        Assert.Equal(WorkOrderErrors.Closed, order.UpdateIntake("Yeni şikâyet", null, null, null).Error);
        Assert.Equal("Ekran kırık", order.Complaint);
    }
}
