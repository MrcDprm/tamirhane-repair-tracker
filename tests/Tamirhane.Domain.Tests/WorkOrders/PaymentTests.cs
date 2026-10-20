using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class PaymentTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    private static BranchStock NoStock(WorkOrder order) => new(order.BranchId, []);

    /// <summary>Toplamı 1.200 TL (1.000 + %20 KDV) olan, müşterinin onayladığı iş emri.</summary>
    private static WorkOrder Approved()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000m, 20, 0);
        order.SendQuote(Staff, Now);
        order.DecideQuote(true, "Ayşe Kaya", null, Now);
        return order;
    }

    private static WorkOrder Ready()
    {
        var order = Approved();
        order.MarkReady(Staff, Now, NoStock(order));
        return order;
    }

    [Fact]
    public void Balance_is_quote_total_minus_payments()
    {
        var order = Approved();

        order.RecordPayment(200m, PaymentMethod.Cash, Staff, Now, "Kapora");
        order.RecordPayment(300m, PaymentMethod.Card, Staff, Now, null);

        Assert.Equal(1200m, order.AmountDue);
        Assert.Equal(500m, order.PaidTotal);
        Assert.Equal(700m, order.Balance);
        Assert.Equal("Kapora", order.Payments[0].Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.005)]
    public void Invalid_amounts_are_rejected(double amount)
    {
        var order = Approved();

        Assert.Equal(PaymentErrors.InvalidAmount,
            order.RecordPayment((decimal)amount, PaymentMethod.Cash, Staff, Now, null).Error);
        Assert.Empty(order.Payments);
    }

    [Fact]
    public void Overpayment_is_rejected()
    {
        var order = Approved();
        order.RecordPayment(1000m, PaymentMethod.Cash, Staff, Now, null);

        Assert.Equal(PaymentErrors.Overpayment, order.RecordPayment(200.01m, PaymentMethod.Cash, Staff, Now, null).Error);
        Assert.True(order.RecordPayment(200m, PaymentMethod.Cash, Staff, Now, null).IsSuccess);
        Assert.Equal(0m, order.Balance);
    }

    [Fact]
    public void Delivery_needs_a_zero_balance()
    {
        var order = Ready();

        Assert.Equal(PaymentErrors.BalanceDue, order.Deliver(Staff, Now).Error);
        Assert.Equal(WorkOrderStatus.Ready, order.Status);

        order.RecordPayment(1200m, PaymentMethod.Transfer, Staff, Now, null);

        Assert.True(order.Deliver(Staff, Now).IsSuccess);
        Assert.Null(order.BalanceAtDelivery);
    }

    [Fact]
    public void Delivery_with_balance_due_is_allowed_with_approval_and_keeps_the_debt()
    {
        var order = Ready();
        order.RecordPayment(1000m, PaymentMethod.Cash, Staff, Now, null);

        Assert.True(order.Deliver(Staff, Now, allowBalanceDue: true).IsSuccess);

        Assert.Equal(WorkOrderStatus.Delivered, order.Status);
        Assert.Equal(200m, order.BalanceAtDelivery);
        Assert.Equal(200m, order.Balance);
    }

    [Fact]
    public void Debt_can_be_paid_after_delivery()
    {
        var order = Ready();
        order.Deliver(Staff, Now, allowBalanceDue: true);

        Assert.True(order.RecordPayment(1200m, PaymentMethod.Cash, Staff, Now.AddDays(3), null).IsSuccess);
        Assert.Equal(0m, order.Balance);
    }

    [Fact]
    public void Paid_order_cannot_be_cancelled_until_refunded()
    {
        var order = Approved();
        order.RecordPayment(200m, PaymentMethod.Cash, Staff, Now, "Kapora");

        Assert.Equal(PaymentErrors.RefundRequired, order.Cancel(Staff, Now, "Müşteri vazgeçti", NoStock(order)).Error);

        Assert.Equal(PaymentErrors.RefundTooLarge, order.RecordRefund(200.01m, PaymentMethod.Cash, Staff, Now, null).Error);
        var refund = order.RecordRefund(200m, PaymentMethod.Cash, Staff, Now, null).Value;

        Assert.Equal(PaymentKind.Refund, refund.Kind);
        Assert.Equal(-200m, refund.SignedAmount);
        Assert.Equal(0m, order.PaidTotal);
        Assert.True(order.Cancel(Staff, Now, "Müşteri vazgeçti", NoStock(order)).IsSuccess);
    }

    [Fact]
    public void Cancelled_order_takes_no_payments()
    {
        var order = Approved();
        order.Cancel(Staff, Now, "Müşteri vazgeçti", NoStock(order));

        Assert.Equal(PaymentErrors.NotAllowed, order.RecordPayment(10m, PaymentMethod.Cash, Staff, Now, null).Error);
    }

    [Fact]
    public void Declined_quote_owes_only_the_diagnosis_fee()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000m, 20, 0);
        Assert.Equal(WorkOrderErrors.InvalidTransition, order.SetDiagnosisFee(150m).Error);
        order.SendQuote(Staff, Now);
        order.DecideQuote(false, "Ayşe Kaya", null, Now);

        Assert.Equal(0m, order.AmountDue);
        Assert.Equal(PaymentErrors.InvalidDiagnosisFee, order.SetDiagnosisFee(-1m).Error);
        Assert.True(order.SetDiagnosisFee(150m).IsSuccess);

        Assert.Equal(150m, order.AmountDue);
        Assert.Equal(PaymentErrors.BalanceDue, order.Deliver(Staff, Now).Error);
        order.RecordPayment(150m, PaymentMethod.Cash, Staff, Now, null);
        Assert.True(order.Deliver(Staff, Now).IsSuccess);
    }

    [Fact]
    public void Deposit_above_the_diagnosis_fee_must_be_refunded_before_delivery()
    {
        var order = WorkOrder.Receive(Guid.NewGuid(), Guid.NewGuid(), "2026-0001", "ABCDEFGH2345",
            Guid.NewGuid(), Guid.NewGuid(), "Ekran kırık", null, null, null, Staff, Now).Value;
        order.StartDiagnosis(Staff, Now);
        order.AddQuoteLine(QuoteLineKind.Labor, null, "Ekran değişimi", 1, 1000m, 20, 0);
        order.SendQuote(Staff, Now);
        order.RecordPayment(200m, PaymentMethod.Cash, Staff, Now, "Kapora");
        order.DecideQuote(false, "Ayşe Kaya", null, Now);

        Assert.True(order.SetDiagnosisFee(100m).IsSuccess);
        Assert.Equal(-100m, order.Balance);
        Assert.Equal(PaymentErrors.RefundDue, order.Deliver(Staff, Now, allowBalanceDue: true).Error);

        order.RecordRefund(100m, PaymentMethod.Cash, Staff, Now, null);

        Assert.Equal(0m, order.Balance);
        Assert.True(order.Deliver(Staff, Now).IsSuccess);
    }
}
