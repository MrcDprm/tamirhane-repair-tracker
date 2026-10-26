using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>Teklif düzenleme: varsayılan fiyat ve KDV, sunucuda hesaplanan toplamlar, gönderince kilit ve bildirim.</summary>
public class QuoteEditingTests
{
    private static async Task<(TestShop Shop, Guid Id, Part Part)> DiagnosingWithPartAsync()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        var part = Part.Create(shop.User.ShopId, "IP13-SCR", "iPhone 13 ekran paneli", 1800m, 2500m, 1).Value;
        shop.Parts.Items.Add(part);
        return (shop, id, part);
    }

    [Fact]
    public async Task Stock_part_without_price_or_vat_takes_the_sale_price_and_shop_vat()
    {
        var (shop, id, part) = await DiagnosingWithPartAsync();

        var result = await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, null, null, 0), default);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(shop.Orders.Items[0].QuoteLines);
        Assert.Equal((2500m, 20m, 3000m), (line.UnitPrice, line.VatRate, line.Total));
    }

    [Fact]
    public async Task Free_line_needs_a_price()
    {
        var (shop, id, _) = await DiagnosingWithPartAsync();

        var result = await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "Ekran değişimi işçiliği", 1, null, null, 0), default);

        Assert.Equal(QuoteErrors.InvalidUnitPrice, result.Error);
        Assert.Empty(shop.Orders.Items[0].QuoteLines);
    }

    [Fact]
    public async Task Page_shows_lines_and_totals_calculated_on_the_server()
    {
        var (shop, id, part) = await DiagnosingWithPartAsync();
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, null, null, 0), default);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "İşçilik", 1, 500m, 20, 10), default);

        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

        Assert.Equal(["iPhone 13 ekran paneli", "İşçilik"], view.QuoteLines.Select(l => l.Description));
        Assert.Equal([true, false], view.QuoteLines.Select(l => l.FromStock));
        // 2500 + (500 - %10) = 2950 net; KDV %20 = 590; toplam 3540
        Assert.Equal((3000m, 50m, 2950m, 590m, 3540m),
            (view.QuoteTotals.Gross, view.QuoteTotals.Discount, view.QuoteTotals.Net, view.QuoteTotals.Vat, view.QuoteTotals.Total));
        Assert.True(view.IsQuoteEditable);
        Assert.Equal(20m, view.DefaultVatRate);
        Assert.Equal("iPhone 13 ekran paneli", Assert.Single(view.PartOptions).Name);
    }

    [Fact]
    public async Task Sending_the_quote_locks_it_and_notifies_the_customer()
    {
        var (shop, id, part) = await DiagnosingWithPartAsync();
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Part, part.Id, null, 1, null, null, 0), default);

        Assert.True((await shop.SendQuote().HandleAsync(id, default)).IsSuccess);

        Assert.Equal(new WorkOrderNotification(shop.User.ShopId, id, WorkOrderStatus.AwaitingApproval), shop.Queue.Sent[^1]);
        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;
        Assert.False(view.IsQuoteEditable);
        Assert.Empty(view.PartOptions);
        var locked = await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "Ek iş", 1, 100m, 20, 0), default);
        Assert.Equal(QuoteErrors.NotEditable, locked.Error);
    }

    [Fact]
    public async Task Removing_a_line_updates_the_totals()
    {
        var (shop, id, _) = await DiagnosingWithPartAsync();
        var line = (await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "İşçilik", 1, 500m, 20, 0), default)).Value;

        Assert.True((await shop.RemoveQuoteLine().HandleAsync(id, line, default)).IsSuccess);

        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;
        Assert.Empty(view.QuoteLines);
        Assert.Equal(0m, view.QuoteTotals.Total);
    }
}
