using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

public class WorkflowTests
{
    private static async Task<(TestShop Shop, Guid Id)> DiagnosingAsync(bool isOwner = true)
    {
        var shop = new TestShop(isOwner);
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        return (shop, id);
    }

    private static AddQuoteLineCommand Labor(Guid id, decimal price = 1000m) =>
        new(id, QuoteLineKind.Labor, null, "İşçilik", 1, price, 20, 0);

    [Fact]
    public async Task Each_status_change_is_saved_and_notified()
    {
        var (shop, id) = await DiagnosingAsync();

        Assert.Equal(
            [WorkOrderStatus.Received, WorkOrderStatus.Diagnosing],
            shop.Queue.Sent.Select(n => n.Status));
        Assert.Equal(2, shop.UnitOfWork.Saves);
        Assert.Equal(WorkOrderStatus.Diagnosing, shop.Orders.Items[0].Status);
        Assert.Equal(shop.User.StaffId, shop.Orders.Items[0].History[^1].StaffId);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Unknown_or_foreign_work_order_is_not_found()
    {
        var shop = new TestShop();
        var foreign = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", TrackingCode.Generate(),
            Guid.NewGuid(), Guid.NewGuid(), "Ekran", null, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow).Value;
        shop.Orders.Items.Add(foreign);

        Assert.Equal(ApplicationErrors.WorkOrderNotFound, (await shop.StartDiagnosis().HandleAsync(Guid.NewGuid(), default)).Error);
        Assert.Equal(ApplicationErrors.WorkOrderNotFound, (await shop.StartDiagnosis().HandleAsync(foreign.Id, default)).Error);
        Assert.Equal(WorkOrderStatus.Received, foreign.Status);
    }

    [Fact]
    public async Task Failed_action_saves_nothing_and_notifies_nobody()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();

        var result = await shop.SendQuote().HandleAsync(id, default);

        Assert.Equal(WorkOrderErrors.InvalidTransition, result.Error);
        Assert.Equal(1, shop.UnitOfWork.Saves);
        Assert.Single(shop.Queue.Sent);
    }

    [Fact]
    public async Task Notes_and_quote_lines_are_saved_without_notifying_the_customer()
    {
        var (shop, id) = await DiagnosingAsync();

        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.AddNote().HandleAsync(id, "Anakartta sıvı izi", visibleToCustomer: false, default);

        Assert.Equal(4, shop.UnitOfWork.Saves);
        Assert.Equal(2, shop.Queue.Sent.Count);
    }

    [Fact]
    public async Task Stock_part_line_takes_its_name_and_must_belong_to_the_shop()
    {
        var (shop, id) = await DiagnosingAsync();
        var part = Part.Create(shop.User.ShopId, "IP13-SCR", "iPhone 13 ekran paneli", 1800m, 2500m, 1).Value;
        var foreign = Part.Create(Guid.NewGuid(), "X", "Başka atölyenin parçası", 1, 1, 0).Value;
        var retired = Part.Create(shop.User.ShopId, "OLD", "Eski parça", 1, 1, 0).Value;
        retired.Deactivate();
        shop.Parts.Items.AddRange([part, foreign, retired]);

        var lineId = (await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, 2500m, 20, 0), default)).Value;
        var foreignResult = await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, foreign.Id, null, 1, 1m, 20, 0), default);
        var retiredResult = await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, retired.Id, null, 1, 1m, 20, 0), default);

        var line = Assert.Single(shop.Orders.Items[0].QuoteLines);
        Assert.Equal(lineId, line.Id);
        Assert.Equal("iPhone 13 ekran paneli", line.Description);
        Assert.Equal(ApplicationErrors.PartNotFound, foreignResult.Error);
        Assert.Equal(ApplicationErrors.PartNotFound, retiredResult.Error);
    }

    [Fact]
    public async Task Customer_approves_from_the_tracking_page_with_a_sloppily_typed_code()
    {
        var (shop, id) = await DiagnosingAsync();
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.SendQuote().HandleAsync(id, default);
        var code = shop.Orders.Items[0].TrackingCode;
        var typed = $"{code[..4].ToLowerInvariant()}-{code[4..8]} {code[8..]}";

        var result = await shop.DecideQuoteByCustomer().HandleAsync(typed, approved: true, "Ayşe Kaya", default);

        Assert.True(result.IsSuccess);
        var order = shop.Orders.Items[0];
        Assert.Equal(WorkOrderStatus.InRepair, order.Status);
        Assert.Null(order.QuoteDecision!.StaffId);
        Assert.Equal(WorkOrderStatus.InRepair, shop.Queue.Sent[^1].Status);
        Assert.Equal([id], shop.Workshop.QuoteDecisions);
    }

    [Fact]
    public async Task Customer_decides_only_once()
    {
        var (shop, id) = await DiagnosingAsync();
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.SendQuote().HandleAsync(id, default);
        var code = shop.Orders.Items[0].TrackingCode;
        await shop.DecideQuoteByCustomer().HandleAsync(code, approved: true, "Ayşe Kaya", default);

        var second = await shop.DecideQuoteByCustomer().HandleAsync(code, approved: false, "Ayşe Kaya", default);

        Assert.Equal(WorkOrderErrors.InvalidTransition, second.Error);
        Assert.Equal(QuoteDecisionKind.Approved, shop.Orders.Items[0].QuoteDecision!.Kind);
        Assert.Single(shop.Workshop.QuoteDecisions);
    }

    [Fact]
    public async Task Simultaneous_second_decision_is_not_saved_and_nobody_is_notified()
    {
        var (shop, id) = await DiagnosingAsync();
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.SendQuote().HandleAsync(id, default);
        var sent = shop.Queue.Sent.Count;
        shop.UnitOfWork.ConflictOnNextSave = true; // aynı anda gelen öbür istek önce kaydetti

        var result = await shop.DecideQuoteByCustomer().HandleAsync(shop.Orders.Items[0].TrackingCode, true, "Ayşe Kaya", default);

        Assert.Equal(ApplicationErrors.ConcurrentChange, result.Error);
        Assert.Equal(sent, shop.Queue.Sent.Count);
        Assert.Empty(shop.Workshop.QuoteDecisions);
    }

    [Fact]
    public async Task Workshop_step_that_loses_a_race_reports_the_change_instead_of_failing()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        shop.UnitOfWork.ConflictOnNextSave = true;

        var result = await shop.StartDiagnosis().HandleAsync(id, default);

        Assert.Equal(ApplicationErrors.ConcurrentChange, result.Error);
    }

    [Fact]
    public async Task Unknown_tracking_code_reveals_nothing()
    {
        var shop = new TestShop();

        var result = await shop.DecideQuoteByCustomer().HandleAsync("NOT-A-REAL-CODE", true, "Ayşe", default);

        Assert.Equal(ApplicationErrors.WorkOrderNotFound, result.Error);
    }

    [Fact]
    public async Task Staff_records_a_phone_decision_under_their_own_name()
    {
        var (shop, id) = await DiagnosingAsync();
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.SendQuote().HandleAsync(id, default);

        await shop.RecordQuoteDecision().HandleAsync(id, approved: false, "Ayşe Kaya", default);

        var order = shop.Orders.Items[0];
        Assert.Equal(WorkOrderStatus.Declined, order.Status);
        Assert.Equal(shop.User.StaffId, order.QuoteDecision!.StaffId);
    }

    [Fact]
    public async Task Ready_consumes_stock_and_records_movements()
    {
        var (shop, id) = await DiagnosingAsync();
        var part = Part.Create(shop.User.ShopId, "IP13-SCR", "iPhone 13 ekran", 1800m, 2500m, 1).Value;
        shop.Parts.Items.Add(part);
        var level = StockLevel.Create(part.Id, shop.User.BranchId);
        level.Receive(2, shop.User.StaffId, DateTimeOffset.UtcNow, null);
        shop.Stock.Levels.Add(level);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, 2500m, 20, 0), default);
        await shop.StartRepair().HandleAsync(id, default);

        var result = await shop.MarkReady().HandleAsync(id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, level.Quantity);
        Assert.Equal(StockMovementReason.Used, Assert.Single(shop.Stock.Movements).Reason);
    }

    [Fact]
    public async Task Missing_stock_keeps_the_order_in_repair_without_saving()
    {
        var (shop, id) = await DiagnosingAsync();
        var part = Part.Create(shop.User.ShopId, "IP13-SCR", "iPhone 13 ekran", 1800m, 2500m, 1).Value;
        shop.Parts.Items.Add(part);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, 2500m, 20, 0), default);
        await shop.StartRepair().HandleAsync(id, default);
        var saves = shop.UnitOfWork.Saves;

        var result = await shop.MarkReady().HandleAsync(id, default);

        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(WorkOrderStatus.InRepair, shop.Orders.Items[0].Status);
        Assert.Equal(saves, shop.UnitOfWork.Saves);
        Assert.Empty(shop.Stock.Movements);
    }

    [Fact]
    public async Task Only_the_owner_can_deliver_with_balance_due()
    {
        var (shop, id) = await DiagnosingAsync(isOwner: false);
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.StartRepair().HandleAsync(id, default);
        await shop.MarkReady().HandleAsync(id, default);

        Assert.Equal(ApplicationErrors.OwnerOnly, (await shop.Deliver().HandleAsync(id, allowBalanceDue: true, default)).Error);
        Assert.Equal(PaymentErrors.BalanceDue, (await shop.Deliver().HandleAsync(id, allowBalanceDue: false, default)).Error);

        shop.User.IsOwner = true;
        Assert.True((await shop.Deliver().HandleAsync(id, allowBalanceDue: true, default)).IsSuccess);
        Assert.Equal(1200m, shop.Orders.Items[0].BalanceAtDelivery);
    }

    [Fact]
    public async Task Delivery_starts_the_shop_warranty()
    {
        var (shop, id) = await DiagnosingAsync();
        await shop.StartRepair().HandleAsync(id, default);
        await shop.MarkReady().HandleAsync(id, default);

        await shop.Deliver().HandleAsync(id, allowBalanceDue: false, default);

        var order = shop.Orders.Items[0];
        Assert.Equal(order.DeliveredAt!.Value.AddDays(shop.Settings.WarrantyDays), order.WarrantyUntil);
        Assert.Equal(WorkOrderStatus.Delivered, shop.Queue.Sent[^1].Status);
    }

    [Fact]
    public async Task Cancel_and_intake_correction_go_through_the_domain_rules()
    {
        var (shop, id) = await DiagnosingAsync();

        Assert.True((await shop.UpdateIntake().HandleAsync(new(id, "Ekran ve kamera", null, null, null), default)).IsSuccess);
        Assert.Equal(WorkOrderErrors.CancelReasonRequired, (await shop.Cancel().HandleAsync(id, " ", default)).Error);
        Assert.True((await shop.Cancel().HandleAsync(id, "Müşteri vazgeçti", default)).IsSuccess);

        Assert.Equal("Ekran ve kamera", shop.Orders.Items[0].Complaint);
        Assert.Equal(WorkOrderStatus.Cancelled, shop.Queue.Sent[^1].Status);
    }
}
