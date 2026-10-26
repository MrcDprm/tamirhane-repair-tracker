using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>İş emri sayfasının tahsilat bölümü: ödemeler, kalan borç, iade ve inceleme ücreti ne zaman açık.</summary>
public class PaymentPageTests
{
    private static async Task<(TestShop Shop, Guid Id)> QuotedAsync(decimal labor = 1000m)
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "İşçilik", 1, labor, 20, 0), default);
        return (shop, id);
    }

    private static async Task<WorkOrderView> ViewAsync(TestShop shop, Guid id) => (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

    [Fact]
    public async Task Partial_payment_leaves_a_balance_and_shows_who_took_it()
    {
        var (shop, id) = await QuotedAsync();
        await shop.RecordPayment().HandleAsync(new(id, 500m, PaymentMethod.Cash, "Kapora"), default);

        var view = await ViewAsync(shop, id);

        Assert.Equal((1200m, 500m, 700m), (view.AmountDue, view.PaidTotal, view.Balance));
        var payment = Assert.Single(view.Payments);
        Assert.Equal((PaymentKind.Payment, 500m, PaymentMethod.Cash, "Mehmet Usta", "Kapora"),
            (payment.Kind, payment.Amount, payment.Method, payment.By, payment.Note));
        Assert.True(view.CanTakePayment);
        Assert.True(view.CanRefund);
    }

    [Fact]
    public async Task Paid_in_full_closes_the_payment_form()
    {
        var (shop, id) = await QuotedAsync();
        await shop.RecordPayment().HandleAsync(new(id, 1200m, PaymentMethod.Card, null), default);

        var view = await ViewAsync(shop, id);

        Assert.Equal(0m, view.Balance);
        Assert.False(view.CanTakePayment);
    }

    [Fact]
    public async Task Declined_quote_opens_the_diagnosis_fee_and_it_becomes_the_amount_due()
    {
        var (shop, id) = await QuotedAsync();
        await shop.SendQuote().HandleAsync(id, default);
        await shop.RecordQuoteDecision().HandleAsync(id, false, "Zeynep Arslan", default);
        Assert.True((await ViewAsync(shop, id)).CanSetDiagnosisFee);

        await shop.SetDiagnosisFee().HandleAsync(id, 150m, default);

        var view = await ViewAsync(shop, id);
        Assert.Equal((150m, 150m), (view.DiagnosisFee, view.AmountDue));
    }

    [Fact]
    public async Task Cancelled_order_takes_no_payment()
    {
        var (shop, id) = await QuotedAsync();
        await shop.Cancel().HandleAsync(id, "Vazgeçti", default);

        Assert.False((await ViewAsync(shop, id)).CanTakePayment);
    }

    [Fact]
    public async Task Owner_can_deliver_with_a_balance_and_the_debt_is_kept()
    {
        var (shop, id) = await QuotedAsync();
        await shop.StartRepair().HandleAsync(id, default);
        await shop.MarkReady().HandleAsync(id, default);

        Assert.True((await shop.Deliver().HandleAsync(id, allowBalanceDue: true, default)).IsSuccess);

        Assert.Equal(1200m, (await ViewAsync(shop, id)).BalanceAtDelivery);
    }
}
