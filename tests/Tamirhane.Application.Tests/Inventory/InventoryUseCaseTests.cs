using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Inventory;

namespace Tamirhane.Application.Tests.Inventory;

public class InventoryUseCaseTests
{
    private static PartCommand Screen(string sku = "IP13-SCR", int critical = 2) =>
        new(sku, "iPhone 13 ekran paneli", 1800m, 2500m, critical);

    [Fact]
    public async Task New_part_gets_a_stock_level_and_opening_stock_as_a_purchase()
    {
        var shop = new TestShop();

        var id = (await shop.AddPart().HandleAsync(Screen(), openingQuantity: 5, default)).Value;

        var level = Assert.Single(shop.Stock.Levels);
        Assert.Equal(id, level.PartId);
        Assert.Equal(shop.Settings.DefaultBranchId, level.BranchId);
        Assert.Equal(5, level.Quantity);
        Assert.Equal(StockMovementReason.Purchase, Assert.Single(shop.Stock.Movements).Reason);
        Assert.Equal(1, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Part_without_opening_stock_records_no_movement()
    {
        var shop = new TestShop();

        await shop.AddPart().HandleAsync(Screen(), openingQuantity: 0, default);

        Assert.Equal(0, Assert.Single(shop.Stock.Levels).Quantity);
        Assert.Empty(shop.Stock.Movements);
    }

    [Fact]
    public async Task Sku_is_unique_within_the_shop_regardless_of_case()
    {
        var shop = new TestShop();
        await shop.AddPart().HandleAsync(Screen("IP13-SCR"), 0, default);

        var duplicate = await shop.AddPart().HandleAsync(Screen("ip13-scr"), 0, default);
        var negative = await shop.AddPart().HandleAsync(Screen("OTHER"), -1, default);

        Assert.Equal(ApplicationErrors.DuplicateSku, duplicate.Error);
        Assert.Equal(InventoryErrors.InvalidQuantity, negative.Error);
        Assert.Single(shop.Parts.Items);
        Assert.Equal(1, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Renaming_a_part_to_an_existing_sku_is_refused()
    {
        var shop = new TestShop();
        await shop.AddPart().HandleAsync(Screen("A1"), 0, default);
        var second = (await shop.AddPart().HandleAsync(Screen("B1"), 0, default)).Value;

        var result = await shop.UpdatePart().HandleAsync(second, Screen("a1"), default);
        var renamed = await shop.UpdatePart().HandleAsync(second, Screen("B2"), default);

        Assert.Equal(ApplicationErrors.DuplicateSku, result.Error);
        Assert.True(renamed.IsSuccess);
        Assert.Equal(ApplicationErrors.PartNotFound, (await shop.UpdatePart().HandleAsync(Guid.NewGuid(), Screen("C1"), default)).Error);
    }

    [Fact]
    public async Task Receiving_and_counting_stock_record_movements()
    {
        var shop = new TestShop();
        var id = (await shop.AddPart().HandleAsync(Screen(), 2, default)).Value;

        await shop.ChangeStock().ReceiveAsync(id, 3, "Fatura 123", default);
        var adjusted = await shop.ChangeStock().AdjustAsync(id, 4, "Biri kırık geldi", default);

        Assert.True(adjusted.IsSuccess);
        Assert.Equal(4, shop.Stock.Levels[0].Quantity);
        Assert.Equal([2, 3, -1], shop.Stock.Movements.Select(m => m.Change));
    }

    [Fact]
    public async Task Stock_changes_on_unknown_parts_or_with_bad_input_fail()
    {
        var shop = new TestShop();
        var id = (await shop.AddPart().HandleAsync(Screen(), 0, default)).Value;
        var saves = shop.UnitOfWork.Saves;

        Assert.Equal(ApplicationErrors.PartNotFound, (await shop.ChangeStock().ReceiveAsync(Guid.NewGuid(), 1, null, default)).Error);
        Assert.Equal(InventoryErrors.InvalidQuantity, (await shop.ChangeStock().ReceiveAsync(id, 0, null, default)).Error);
        Assert.Equal(InventoryErrors.ReasonRequired, (await shop.ChangeStock().AdjustAsync(id, 3, " ", default)).Error);
        Assert.Equal(saves, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Low_stock_lists_parts_at_or_below_critical_level_lowest_first()
    {
        var shop = new TestShop();
        await shop.AddPart().HandleAsync(new("A", "Pil", 1, 1, CriticalLevel: 3), openingQuantity: 3, default);
        await shop.AddPart().HandleAsync(new("B", "Ekran", 1, 1, CriticalLevel: 3), openingQuantity: 1, default);
        await shop.AddPart().HandleAsync(new("C", "Kablo", 1, 1, CriticalLevel: 3), openingQuantity: 10, default);
        await shop.AddPart().HandleAsync(new("D", "Vida", 1, 1, CriticalLevel: 0), openingQuantity: 0, default);
        var retired = (await shop.AddPart().HandleAsync(new("E", "Eski", 1, 1, CriticalLevel: 5), openingQuantity: 0, default)).Value;
        shop.Parts.Items.Find(p => p.Id == retired)!.Deactivate();

        var low = await shop.ListLowStock().HandleAsync(default);

        Assert.Equal(["Ekran", "Pil"], low.Select(l => l.Name));
        Assert.Equal(1, low[0].Quantity);
    }
}
