using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;
using A = Tamirhane.Application.WorkOrders.WorkOrderAction;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>İş emri sayfası: görünen işlemler durum makinesinden gelir, geçmiş ve notlar kimin yaptığıyla.</summary>
public class WorkOrderPageTests
{
    private static async Task<WorkOrderView> ViewAsync(TestShop shop, Guid id) => (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

    private static AddQuoteLineCommand Labor(Guid id) => new(id, QuoteLineKind.Labor, null, "İşçilik", 1, 1000m, 20, 0);

    [Fact]
    public async Task Received_order_offers_diagnosis_first_and_cancel_last()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();

        var view = await ViewAsync(shop, id);

        Assert.Equal([A.StartDiagnosis, A.Cancel], view.Actions);
        Assert.Equal(A.StartDiagnosis, view.NextAction);
    }

    [Fact]
    public async Task Diagnosis_without_quote_lines_goes_straight_to_repair()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);

        Assert.Equal([A.StartRepair, A.Cancel], (await ViewAsync(shop, id)).Actions);
    }

    [Fact]
    public async Task Diagnosis_with_quote_lines_offers_sending_the_quote_first()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);

        var view = await ViewAsync(shop, id);

        Assert.Equal([A.SendQuote, A.StartRepair, A.Cancel], view.Actions);
        Assert.Equal(1, view.QuoteLineCount);
        Assert.Equal(1200m, view.AmountDue);
    }

    [Fact]
    public async Task Waiting_for_approval_offers_both_decisions()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.AddQuoteLine().HandleAsync(Labor(id), default);
        await shop.SendQuote().HandleAsync(id, default);

        Assert.Equal([A.RecordApproval, A.RecordDecline, A.Cancel], (await ViewAsync(shop, id)).Actions);
    }

    [Fact]
    public async Task Repair_then_ready_then_delivery()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.StartRepair().HandleAsync(id, default);
        Assert.Equal([A.MarkReady, A.Cancel], (await ViewAsync(shop, id)).Actions);

        await shop.MarkReady().HandleAsync(id, default);
        Assert.Equal([A.Deliver, A.Cancel], (await ViewAsync(shop, id)).Actions);
    }

    [Fact]
    public async Task Closed_order_offers_nothing()
    {
        var shop = new TestShop();
        var cancelled = await shop.ReceiveAsync("0532 000 00 01");
        await shop.Cancel().HandleAsync(cancelled, "Müşteri vazgeçti", default);
        var delivered = await shop.ReceiveAsync("0532 000 00 02");
        await shop.StartDiagnosis().HandleAsync(delivered, default);
        await shop.StartRepair().HandleAsync(delivered, default);
        await shop.MarkReady().HandleAsync(delivered, default);
        await shop.Deliver().HandleAsync(delivered, false, default);

        Assert.Empty((await ViewAsync(shop, cancelled)).Actions);
        var view = await ViewAsync(shop, delivered);
        Assert.Empty(view.Actions);
        Assert.Null(view.NextAction);
        Assert.NotNull(view.WarrantyUntil);
    }

    [Fact]
    public async Task History_lists_every_step_with_who_did_it_and_the_cancel_reason()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.Cancel().HandleAsync(id, "Parça bulunamadı", default);

        var history = (await ViewAsync(shop, id)).History;

        Assert.Equal([WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, WorkOrderStatus.Cancelled], history.Select(h => h.Status));
        Assert.All(history, h => Assert.Equal("Mehmet Usta", h.By));
        Assert.Equal("Parça bulunamadı", history[^1].Note);
    }

    [Fact]
    public async Task Workshop_and_customer_notes_are_kept_apart_newest_first()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.AddNote().HandleAsync(id, "Anakart nemli, kurutmaya alındı", false, default);
        shop.Clock.Advance(TimeSpan.FromMinutes(5));
        await shop.AddNote().HandleAsync(id, "Parça siparişi verildi, Perşembe gelecek", true, default);

        var notes = (await ViewAsync(shop, id)).Notes;

        Assert.Equal(["Parça siparişi verildi, Perşembe gelecek", "Anakart nemli, kurutmaya alındı"], notes.Select(n => n.Text));
        Assert.Equal([true, false], notes.Select(n => n.VisibleToCustomer));
        Assert.All(notes, n => Assert.Equal("Mehmet Usta", n.By));
    }
}
