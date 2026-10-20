using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.Inventory;

public class InventoryTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static StockLevel LevelWith(Guid partId, int quantity, Guid? branch = null)
    {
        var level = StockLevel.Create(partId, branch ?? Branch);
        if (quantity > 0)
            level.Receive(quantity, Staff, Now, null);
        return level;
    }

    private static WorkOrder InRepairWithParts(params (Guid PartId, int Quantity)[] parts)
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Branch, "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);
        foreach (var (partId, quantity) in parts)
            order.AddQuoteLine(QuoteLineKind.Part, partId, "Parça", quantity, 100m, 20, 0);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "İşçilik", 1, 300m, 20, 0);
        order.SendQuote(Staff, Now);
        order.DecideQuote(true, "Ayşe Kaya", null, Now);
        return order;
    }

    [Fact]
    public void Part_normalizes_sku_and_validates_fields()
    {
        var part = Part.Create(Guid.NewGuid(), " ip13-scr ", " iPhone 13 ekran ", 1800m, 2500m, 2).Value;

        Assert.Equal("IP13-SCR", part.Sku);
        Assert.Equal("iPhone 13 ekran", part.Name);
        Assert.True(part.IsActive);
        Assert.Equal(InventoryErrors.InvalidSku, Part.Create(Guid.NewGuid(), " ", "Ad", 1, 1, 0).Error);
        Assert.Equal(InventoryErrors.InvalidName, Part.Create(Guid.NewGuid(), "A1", "", 1, 1, 0).Error);
        Assert.Equal(InventoryErrors.InvalidPrice, Part.Create(Guid.NewGuid(), "A1", "Ad", -1, 1, 0).Error);
        Assert.Equal(InventoryErrors.InvalidPrice, Part.Create(Guid.NewGuid(), "A1", "Ad", 1, 1.005m, 0).Error);
        Assert.Equal(InventoryErrors.InvalidCriticalLevel, Part.Create(Guid.NewGuid(), "A1", "Ad", 1, 1, -1).Error);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 3, false)]
    [InlineData(2, 2, true)]
    [InlineData(2, 0, true)]
    public void Part_is_low_at_or_below_its_critical_level(int critical, int quantity, bool expected)
    {
        var part = Part.Create(Guid.NewGuid(), "A1", "Ad", 1, 1, critical).Value;

        Assert.Equal(expected, part.IsLow(quantity));
    }

    [Fact]
    public void Receive_adds_stock_and_records_a_purchase()
    {
        var level = StockLevel.Create(Guid.NewGuid(), Branch);

        var movement = level.Receive(5, Staff, Now, " Fatura 123 ").Value;

        Assert.Equal(5, level.Quantity);
        Assert.Equal(5, movement.Change);
        Assert.Equal(StockMovementReason.Purchase, movement.Reason);
        Assert.Equal("Fatura 123", movement.Note);
        Assert.Equal(InventoryErrors.InvalidQuantity, level.Receive(0, Staff, Now, null).Error);
    }

    [Fact]
    public void Adjust_sets_the_counted_quantity_and_needs_a_reason()
    {
        var level = LevelWith(Guid.NewGuid(), 5);

        Assert.Equal(InventoryErrors.ReasonRequired, level.Adjust(3, Staff, Now, " ").Error);
        Assert.Equal(InventoryErrors.InvalidQuantity, level.Adjust(-1, Staff, Now, "Sayım").Error);
        Assert.Equal(InventoryErrors.InvalidQuantity, level.Adjust(5, Staff, Now, "Sayım").Error);

        var movement = level.Adjust(3, Staff, Now, "Biri kırıldı").Value;

        Assert.Equal(3, level.Quantity);
        Assert.Equal(-2, movement.Change);
        Assert.Equal(StockMovementReason.Adjustment, movement.Reason);
    }

    [Fact]
    public void Ready_consumes_quoted_parts_from_the_branch()
    {
        var screen = Guid.NewGuid();
        var battery = Guid.NewGuid();
        var order = InRepairWithParts((screen, 1), (battery, 2));
        var stock = new BranchStock(Branch, [LevelWith(screen, 3), LevelWith(battery, 2)]);

        var movements = order.MarkReady(Staff, Now, stock).Value;

        Assert.Equal(WorkOrderStatus.Ready, order.Status);
        Assert.True(order.PartsConsumed);
        Assert.Equal(2, stock.QuantityOf(screen));
        Assert.Equal(0, stock.QuantityOf(battery));
        Assert.All(movements, m => Assert.Equal(StockMovementReason.Used, m.Reason));
        Assert.All(movements, m => Assert.Equal(order.Id, m.WorkOrderId));
        Assert.Equal(-3, movements.Sum(m => m.Change));
    }

    [Fact]
    public void Same_part_on_two_lines_is_counted_together()
    {
        var screw = Guid.NewGuid();
        var order = InRepairWithParts((screw, 2), (screw, 2));
        var stock = new BranchStock(Branch, [LevelWith(screw, 3)]);

        Assert.Equal(InventoryErrors.InsufficientStock, order.MarkReady(Staff, Now, stock).Error);
        Assert.Equal(3, stock.QuantityOf(screw));
    }

    [Fact]
    public void Insufficient_stock_keeps_the_order_in_repair_and_changes_nothing()
    {
        var screen = Guid.NewGuid();
        var battery = Guid.NewGuid();
        var order = InRepairWithParts((screen, 1), (battery, 2));
        var stock = new BranchStock(Branch, [LevelWith(screen, 3), LevelWith(battery, 1)]);

        var result = order.MarkReady(Staff, Now, stock);

        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(WorkOrderStatus.InRepair, order.Status);
        Assert.False(order.PartsConsumed);
        Assert.Equal(3, stock.QuantityOf(screen));
        Assert.Equal(1, stock.QuantityOf(battery));
    }

    [Fact]
    public void Stock_of_another_branch_is_rejected()
    {
        var screen = Guid.NewGuid();
        var order = InRepairWithParts((screen, 1));
        var otherBranch = Guid.NewGuid();
        var stock = new BranchStock(otherBranch, [LevelWith(screen, 3, otherBranch)]);

        Assert.Equal(InventoryErrors.WrongBranch, order.MarkReady(Staff, Now, stock).Error);
        Assert.Equal(WorkOrderStatus.InRepair, order.Status);
    }

    [Fact]
    public void Lines_without_a_stock_part_do_not_touch_stock()
    {
        var order = InRepairWithParts();
        order.AddQuoteLine(QuoteLineKind.Part, null, "Sipariş edilen parça", 1, 500m, 20, 0);

        var movements = order.MarkReady(Staff, Now, new BranchStock(Branch, [])).Value;

        Assert.Empty(movements);
        Assert.Equal(WorkOrderStatus.Ready, order.Status);
    }

    [Fact]
    public void Cancelling_a_ready_order_returns_its_parts()
    {
        var screen = Guid.NewGuid();
        var order = InRepairWithParts((screen, 1));
        var stock = new BranchStock(Branch, [LevelWith(screen, 3)]);
        order.MarkReady(Staff, Now, stock);

        var movements = order.Cancel(Staff, Now, "Müşteri cihazı almadı", stock).Value;

        Assert.Equal(WorkOrderStatus.Cancelled, order.Status);
        Assert.Equal(3, stock.QuantityOf(screen));
        var movement = Assert.Single(movements);
        Assert.Equal(StockMovementReason.Returned, movement.Reason);
        Assert.Equal(1, movement.Change);
        Assert.False(order.PartsConsumed);
    }

    [Fact]
    public void Cancelling_before_ready_returns_nothing()
    {
        var screen = Guid.NewGuid();
        var order = InRepairWithParts((screen, 1));
        var stock = new BranchStock(Branch, [LevelWith(screen, 3)]);

        var movements = order.Cancel(Staff, Now, "Parça bulunamadı", stock).Value;

        Assert.Empty(movements);
        Assert.Equal(3, stock.QuantityOf(screen));
    }

    [Fact]
    public void Stock_parts_must_be_whole_units()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Branch, "2026-0002", "ABCDEFGH2346",
            Guid.NewGuid(), Guid.NewGuid(), "Pil", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);

        Assert.Equal(QuoteErrors.PartQuantityMustBeWhole,
            order.AddQuoteLine(QuoteLineKind.Part, Guid.NewGuid(), "Kablo", 1.5m, 10m, 20, 0).Error);
        Assert.True(order.AddQuoteLine(QuoteLineKind.Part, null, "Kablo (metre)", 1.5m, 10m, 20, 0).IsSuccess);
    }
}
