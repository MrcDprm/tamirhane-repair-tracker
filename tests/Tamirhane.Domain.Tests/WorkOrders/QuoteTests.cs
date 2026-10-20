using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class QuoteTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static WorkOrder Diagnosing()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Start).Value;
        order.StartDiagnosis(Staff, Start.AddHours(1));
        return order;
    }

    private static WorkOrder AwaitingApproval()
    {
        var order = Diagnosing();
        order.AddQuoteLine(QuoteLineKind.Part, Guid.NewGuid(), "Ekran paneli", 1, 2500m, 20, 0);
        order.SendQuote(Staff, Start.AddHours(2));
        return order;
    }

    [Fact]
    public void Line_amounts_follow_gross_discount_net_vat_order()
    {
        var order = Diagnosing();

        var line = order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1.5m, 400m, 20, 10).Value;

        Assert.Equal(600m, line.Gross);
        Assert.Equal(60m, line.Discount);
        Assert.Equal(540m, line.Net);
        Assert.Equal(108m, line.Vat);
        Assert.Equal(648m, line.Total);
    }

    [Fact]
    public void Half_kurus_rounds_away_from_zero()
    {
        var order = Diagnosing();

        // 0,25 × %10 = 0,025 → 0,03
        var line = order.AddQuoteLine(QuoteLineKind.Part, null, "Vida", 1, 0.25m, 10, 0).Value;

        Assert.Equal(0.03m, line.Vat);
        Assert.Equal(0.28m, line.Total);
    }

    [Fact]
    public void Totals_are_the_sum_of_rounded_lines()
    {
        var order = Diagnosing();
        order.AddQuoteLine(QuoteLineKind.Part, Guid.NewGuid(), "Ekran paneli", 1, 2500m, 20, 0);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 500m, 20, 100);

        var totals = order.QuoteTotals;

        Assert.Equal(3000m, totals.Gross);
        Assert.Equal(500m, totals.Discount);
        Assert.Equal(2500m, totals.Net);
        Assert.Equal(500m, totals.Vat);
        Assert.Equal(3000m, totals.Total);
    }

    [Fact]
    public void Labor_lines_never_point_to_a_part()
    {
        var order = Diagnosing();

        var line = order.AddQuoteLine(QuoteLineKind.Labor, Guid.NewGuid(), "İşçilik", 1, 100m, 20, 0).Value;

        Assert.Null(line.PartId);
    }

    [Theory]
    [InlineData("", 1, 100, 20, 0)]
    [InlineData("Ekran", 0, 100, 20, 0)]
    [InlineData("Ekran", -1, 100, 20, 0)]
    [InlineData("Ekran", 1.555, 100, 20, 0)]
    [InlineData("Ekran", 1, -5, 20, 0)]
    [InlineData("Ekran", 1, 10.001, 20, 0)]
    [InlineData("Ekran", 1, 100, 101, 0)]
    [InlineData("Ekran", 1, 100, -1, 0)]
    [InlineData("Ekran", 1, 100, 20, 100.5)]
    [InlineData("Ekran", 1, 100, 20, -1)]
    public void Invalid_lines_are_rejected(string description, double quantity, double price, double vat, double discount)
    {
        var order = Diagnosing();

        var result = order.AddQuoteLine(QuoteLineKind.Part, null, description,
            (decimal)quantity, (decimal)price, (decimal)vat, (decimal)discount);

        Assert.True(result.IsFailure);
        Assert.Empty(order.QuoteLines);
    }

    [Fact]
    public void Lines_cannot_be_added_before_diagnosis()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Start).Value;

        var result = order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 100m, 20, 0);

        Assert.Equal(QuoteErrors.NotEditable, result.Error);
    }

    [Fact]
    public void Remove_line_works_while_editable_and_reports_unknown_lines()
    {
        var order = Diagnosing();
        var line = order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 100m, 20, 0).Value;

        Assert.Equal(QuoteErrors.LineNotFound, order.RemoveQuoteLine(Guid.NewGuid()).Error);
        Assert.True(order.RemoveQuoteLine(line.Id).IsSuccess);
        Assert.Empty(order.QuoteLines);
    }

    [Fact]
    public void Empty_quote_cannot_be_sent()
    {
        var order = Diagnosing();

        Assert.Equal(QuoteErrors.Empty, order.SendQuote(Staff, Start).Error);
        Assert.Equal(WorkOrderStatus.Diagnosing, order.Status);
    }

    [Fact]
    public void Sent_quote_is_locked()
    {
        var order = AwaitingApproval();

        Assert.Equal(QuoteErrors.NotEditable,
            order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 100m, 20, 0).Error);
        Assert.Equal(QuoteErrors.NotEditable, order.RemoveQuoteLine(order.QuoteLines[0].Id).Error);
    }

    [Fact]
    public void Approval_from_tracking_page_starts_repair_and_locks_the_quote()
    {
        var order = AwaitingApproval();

        Assert.True(order.DecideQuote(true, " Ayşe Kaya ", null, Start.AddHours(3)).IsSuccess);

        Assert.Equal(WorkOrderStatus.InRepair, order.Status);
        Assert.Equal(new QuoteDecision(QuoteDecisionKind.Approved, "Ayşe Kaya", Start.AddHours(3), null), order.QuoteDecision);
        Assert.Null(order.History[^1].StaffId);
        Assert.False(order.IsQuoteEditable);
    }

    [Fact]
    public void Staff_can_record_a_decision_given_by_phone()
    {
        var order = AwaitingApproval();

        order.DecideQuote(false, "Ayşe Kaya", Staff, Start.AddHours(3));

        Assert.Equal(WorkOrderStatus.Declined, order.Status);
        Assert.Equal(Staff, order.QuoteDecision!.StaffId);
        Assert.Equal(Staff, order.History[^1].StaffId);
    }

    [Fact]
    public void Declined_device_can_be_returned_unrepaired()
    {
        var order = AwaitingApproval();
        order.DecideQuote(false, "Ayşe Kaya", null, Start.AddHours(3));

        Assert.True(order.Deliver(Staff, Start.AddDays(1)).IsSuccess);
        Assert.Equal(WorkOrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void Decision_needs_a_name_and_can_be_given_once()
    {
        var order = AwaitingApproval();

        Assert.Equal(QuoteErrors.DeciderNameRequired, order.DecideQuote(true, "  ", null, Start).Error);
        Assert.True(order.DecideQuote(true, "Ayşe Kaya", null, Start).IsSuccess);
        Assert.Equal(WorkOrderErrors.InvalidTransition, order.DecideQuote(false, "Ayşe Kaya", null, Start).Error);
        Assert.Equal(QuoteDecisionKind.Approved, order.QuoteDecision!.Kind);
    }

    [Fact]
    public void Repair_without_quote_still_accepts_used_parts()
    {
        var order = Diagnosing();
        order.StartRepair(Staff, Start.AddHours(2));

        var result = order.AddQuoteLine(QuoteLineKind.Part, Guid.NewGuid(), "Şarj soketi", 1, 150m, 20, 100);

        Assert.True(result.IsSuccess);
        Assert.True(order.IsQuoteEditable);
    }

    [Fact]
    public void Quote_cannot_be_sent_from_any_other_status()
    {
        var order = Diagnosing();
        order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 100m, 20, 0);
        order.StartRepair(Staff, Start.AddHours(2));

        Assert.Equal(WorkOrderErrors.InvalidTransition, order.SendQuote(Staff, Start).Error);
    }
}
