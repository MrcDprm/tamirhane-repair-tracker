using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.Inventory;

/// <summary>Stok sayfası: arama, kritik stok, kullanımdan kalkan parça, hareket geçmişi ve yetkiler.</summary>
public class StockPageTests
{
    private static async Task<TestShop> ShopWithPartsAsync(bool isOwner = true)
    {
        var shop = new TestShop();
        await shop.AddPart().HandleAsync(new("IP13-SCR", "iPhone 13 ekran paneli", 1800m, 2500m, 2), 1, default);
        await shop.AddPart().HandleAsync(new("SMS-BAT", "Samsung A54 batarya", 400m, 900m, 1), 5, default);
        await shop.AddPart().HandleAsync(new("USB-C", "Şarj soketi USB-C", 50m, 150m, 0), 0, default);
        shop.User.IsOwner = isOwner;
        return shop;
    }

    [Theory]
    [InlineData("ip13", "iPhone 13 ekran paneli")] // Türkçe kurala göre "i" ≠ "I"; kodlar yine bulunur
    [InlineData("EKRAN", "iPhone 13 ekran paneli")]
    [InlineData("şarj", "Şarj soketi USB-C")]
    [InlineData("ŞARJ", "Şarj soketi USB-C")]
    [InlineData("bat", "Samsung A54 batarya")]
    public async Task Search_matches_code_or_name_ignoring_case(string query, string expected)
    {
        var shop = await ShopWithPartsAsync();

        var list = await shop.ListParts().HandleAsync(query, false, false, default);

        Assert.Equal([expected], list.Parts.Select(p => p.Name));
    }

    [Fact]
    public async Task Low_stock_comes_first_and_can_be_filtered()
    {
        var shop = await ShopWithPartsAsync();

        var all = await shop.ListParts().HandleAsync(null, false, false, default);
        var low = await shop.ListParts().HandleAsync(null, true, false, default);

        Assert.Equal("iPhone 13 ekran paneli", all.Parts[0].Name); // 1 adet, kritik seviye 2
        Assert.True(all.Parts[0].IsLow);
        Assert.Equal(["iPhone 13 ekran paneli"], low.Parts.Select(p => p.Name));
        Assert.Equal((3, 1), (all.ActiveCount, all.LowCount));
    }

    [Fact]
    public async Task Retired_part_is_hidden_until_asked_for()
    {
        var shop = await ShopWithPartsAsync();
        var id = shop.Parts.Items.Single(p => p.Sku == "USB-C").Id;

        Assert.True((await shop.SetPartActive().HandleAsync(id, false, default)).IsSuccess);

        Assert.DoesNotContain((await shop.ListParts().HandleAsync(null, false, false, default)).Parts, p => p.Id == id);
        Assert.False(Assert.Single((await shop.ListParts().HandleAsync(null, false, true, default)).Parts, p => p.Id == id).IsActive);
    }

    [Fact]
    public async Task Technician_sees_stock_but_not_cost_and_cannot_manage_parts()
    {
        var shop = await ShopWithPartsAsync(isOwner: false);
        var part = shop.Parts.Items[0];

        Assert.All((await shop.ListParts().HandleAsync(null, false, false, default)).Parts, p => Assert.Null(p.CostPrice));
        Assert.Null((await shop.GetPart().HandleAsync(part.Id, default)).Value.Part.CostPrice);
        Assert.Equal(ApplicationErrors.OwnerOnly, (await shop.AddPart().HandleAsync(new("X", "Yeni", 1, 2, 0), 0, default)).Error);
        Assert.Equal(ApplicationErrors.OwnerOnly, (await shop.UpdatePart().HandleAsync(part.Id, new("X", "Değişti", 1, 2, 0), default)).Error);
        Assert.Equal(ApplicationErrors.OwnerOnly, (await shop.SetPartActive().HandleAsync(part.Id, false, default)).Error);
        // Stok girişi ve sayım teknisyenin de işi.
        Assert.True((await shop.ChangeStock().ReceiveAsync(part.Id, 3, "Tedarikçi teslimatı", default)).IsSuccess);
    }

    [Fact]
    public async Task Part_page_lists_movements_newest_first_with_who_and_which_work_order()
    {
        var shop = await ShopWithPartsAsync();
        var part = shop.Parts.Items.Single(p => p.Sku == "SMS-BAT");
        shop.Clock.Advance(TimeSpan.FromMinutes(1));
        await shop.ChangeStock().AdjustAsync(part.Id, 4, "Sayım farkı", default);
        shop.Clock.Advance(TimeSpan.FromMinutes(1));
        var order = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(order, default);
        await shop.AddQuoteLine().HandleAsync(new(order, QuoteLineKind.Part, part.Id, null, 1, null, null, 0), default);
        await shop.StartRepair().HandleAsync(order, default);
        await shop.MarkReady().HandleAsync(order, default);

        var detail = (await shop.GetPart().HandleAsync(part.Id, default)).Value;

        Assert.Equal(3, detail.Part.Quantity);
        Assert.Equal([StockMovementReason.Used, StockMovementReason.Adjustment, StockMovementReason.Purchase], detail.Movements.Select(m => m.Reason));
        Assert.Equal([-1, -1, 5], detail.Movements.Select(m => m.Change));
        Assert.Equal("2026-0001", detail.Movements[0].WorkOrderNumber);
        Assert.Equal("Sayım farkı", detail.Movements[1].Note);
        Assert.All(detail.Movements, m => Assert.Equal("Mehmet Usta", m.By));
    }

    [Fact]
    public async Task Another_shops_part_is_not_found()
    {
        var other = await ShopWithPartsAsync();
        var shop = new TestShop();
        shop.Parts.Items.AddRange(other.Parts.Items);

        Assert.Equal(ApplicationErrors.PartNotFound, (await shop.GetPart().HandleAsync(other.Parts.Items[0].Id, default)).Error);
        Assert.Empty((await shop.ListParts().HandleAsync(null, false, true, default)).Parts);
    }
}
