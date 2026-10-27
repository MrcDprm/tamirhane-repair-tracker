using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

public class PaymentAndWarrantyTests
{
    /// <summary>1.200 TL'lik teklifi onaylanmış iş emri.</summary>
    private static async Task<(TestShop Shop, Guid Id)> ApprovedAsync(bool isOwner = true)
    {
        var shop = new TestShop(isOwner);
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000m, 20, 0), default);
        await shop.SendQuote().HandleAsync(id, default);
        await shop.RecordQuoteDecision().HandleAsync(id, approved: true, "Ayşe Kaya", default);
        return (shop, id);
    }

    private static async Task<(TestShop Shop, Guid Id)> DeliveredAsync()
    {
        var (shop, id) = await ApprovedAsync();
        await shop.MarkReady().HandleAsync(id, default);
        await shop.RecordPayment().HandleAsync(new(id, 1200m, PaymentMethod.Card, null), default);
        await shop.Deliver().HandleAsync(id, allowBalanceDue: false, default);
        return (shop, id);
    }

    [Fact]
    public async Task Payment_reduces_the_balance_and_is_saved()
    {
        var (shop, id) = await ApprovedAsync();
        var saves = shop.UnitOfWork.Saves;

        var paymentId = (await shop.RecordPayment().HandleAsync(new(id, 500m, PaymentMethod.Cash, "Kapora"), default)).Value;

        var order = shop.Orders.Items[0];
        Assert.Equal(paymentId, order.Payments[0].Id);
        Assert.Equal(shop.User.StaffId, order.Payments[0].StaffId);
        Assert.Equal(700m, order.Balance);
        Assert.Equal(saves + 1, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Overpayment_is_refused_without_saving()
    {
        var (shop, id) = await ApprovedAsync();
        var saves = shop.UnitOfWork.Saves;

        var result = await shop.RecordPayment().HandleAsync(new(id, 1500m, PaymentMethod.Cash, null), default);

        Assert.Equal(PaymentErrors.Overpayment, result.Error);
        Assert.Equal(saves, shop.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Only_the_owner_can_refund()
    {
        var (shop, id) = await ApprovedAsync(isOwner: false);
        await shop.RecordPayment().HandleAsync(new(id, 500m, PaymentMethod.Cash, null), default);

        var refused = await shop.RecordRefund().HandleAsync(new(id, 500m, PaymentMethod.Cash, null), default);
        shop.User.IsOwner = true;
        var refunded = await shop.RecordRefund().HandleAsync(new(id, 500m, PaymentMethod.Cash, null), default);

        Assert.Equal(ApplicationErrors.OwnerOnly, refused.Error);
        Assert.True(refunded.IsSuccess);
        Assert.Equal(0m, shop.Orders.Items[0].PaidTotal);
    }

    [Fact]
    public async Task Diagnosis_fee_applies_after_a_declined_quote()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync();
        await shop.StartDiagnosis().HandleAsync(id, default);
        await shop.AddQuoteLine().HandleAsync(new(id, QuoteLineKind.Labor, null, "Anakart", 1, 3000m, 20, 0), default);
        await shop.SendQuote().HandleAsync(id, default);
        await shop.RecordQuoteDecision().HandleAsync(id, approved: false, "Ayşe Kaya", default);

        Assert.True((await shop.SetDiagnosisFee().HandleAsync(id, 250m, default)).IsSuccess);
        Assert.Equal(250m, shop.Orders.Items[0].Balance);
    }

    [Fact]
    public async Task Warranty_return_opens_a_linked_order_for_the_same_customer_and_device()
    {
        var (shop, previousId) = await DeliveredAsync();
        shop.Clock.Advance(TimeSpan.FromDays(30));

        var received = (await shop.ReopenUnderWarranty().HandleAsync(new(previousId, "Ekran yine kararıyor", null, null, null), default)).Value;

        var previous = shop.Orders.Items[0];
        var order = shop.Orders.Items[1];
        Assert.Equal(received.WorkOrderId, order.Id);
        Assert.Equal(previousId, order.WarrantyOfId);
        Assert.Equal(previous.CustomerId, order.CustomerId);
        Assert.Equal(previous.DeviceId, order.DeviceId);
        Assert.Equal("2026-0002", received.Number);
        Assert.Equal(WorkOrderStatus.Received, shop.Queue.Sent[^1].Status);
    }

    [Fact]
    public async Task Expired_warranty_cannot_be_reopened()
    {
        var (shop, previousId) = await DeliveredAsync();
        shop.Clock.Advance(TimeSpan.FromDays(shop.Settings.WarrantyDays + 1));

        var result = await shop.ReopenUnderWarranty().HandleAsync(new(previousId, "Ekran yine kararıyor", null, null, null), default);

        Assert.Equal(WorkOrderErrors.NotUnderWarranty, result.Error);
        Assert.Single(shop.Orders.Items);
    }

    [Fact]
    public async Task Unknown_previous_order_is_not_found()
    {
        var shop = new TestShop();

        var result = await shop.ReopenUnderWarranty().HandleAsync(new(Guid.NewGuid(), "Sorun", null, null, null), default);

        Assert.Equal(ApplicationErrors.WorkOrderNotFound, result.Error);
    }

    [Fact]
    public async Task Work_order_pages_link_the_warranty_return_and_the_previous_order_both_ways()
    {
        var (shop, previousId) = await DeliveredAsync();
        Assert.True((await shop.GetWorkOrder().HandleAsync(previousId, default)).Value.CanReopenUnderWarranty);

        var returned = (await shop.ReopenUnderWarranty().HandleAsync(new(previousId, "Ekran yine kararıyor", null, null, null), default)).Value;

        var previous = (await shop.GetWorkOrder().HandleAsync(previousId, default)).Value;
        var current = (await shop.GetWorkOrder().HandleAsync(returned.WorkOrderId, default)).Value;
        Assert.Equal(new WorkOrderLink(previousId, previous.Number), current.WarrantyOf);
        Assert.True(current.IsWarrantyReturn);
        Assert.False(current.CanReopenUnderWarranty); // açık iş emrinden yeni garanti kaydı açılmaz
        Assert.Equal([new WorkOrderLink(returned.WorkOrderId, returned.Number)], previous.WarrantyReturns);
    }

    [Fact]
    public async Task After_the_warranty_ends_no_new_warranty_record_is_offered()
    {
        var (shop, previousId) = await DeliveredAsync();
        shop.Clock.Advance(TimeSpan.FromDays(shop.Settings.WarrantyDays + 1));

        var view = (await shop.GetWorkOrder().HandleAsync(previousId, default)).Value;

        Assert.False(view.CanReopenUnderWarranty);
        Assert.Null(view.WarrantyOf);
    }
}
