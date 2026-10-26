using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Tahsilat HTTP üzerinden: kısmi ödeme ve kalan borç, fazla ödeme, iade ve borçlu teslimde sahip onayı.</summary>
[Collection(WebCollection.Name)]
public sealed class PaymentFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Partial_payment_leaves_a_balance_and_overpayment_is_refused()
    {
        var owner = await _flows.OwnerAsync("odeme-kismi@example.com");
        var order = await QuotedAsync(owner, "0532 121 21 21");

        await PayAsync(owner, order, "500", "Cash", "Kapora");
        var page = await BodyAsync(owner, order);
        Assert.Contains("500,00 tahsilat kaydedildi.", page);
        Assert.Contains("Kalan borç", page);
        Assert.Contains("700,00", page); // 1200 - 500
        Assert.Contains("Kapora", page);

        await PayAsync(owner, order, "800", "Card");
        Assert.Contains("Kalan borçtan fazla tahsilat alınamaz.", await BodyAsync(owner, order));

        await PayAsync(owner, order, "700", "Card");
        page = await BodyAsync(owner, order);
        Assert.Contains("Ödendi", page);
        Assert.DoesNotContain("Tahsilatı kaydet", page);
    }

    [DockerFact]
    public async Task Delivering_with_a_balance_needs_the_owner()
    {
        var owner = await _flows.OwnerAsync("odeme-borclu@example.com");
        var technician = await _flows.TechnicianAsync(owner, "odeme-borclu-usta@example.com");
        var order = await ReadyAsync(owner, "0532 131 31 31");

        var techPage = await BodyAsync(technician, order);
        Assert.Contains("Kalan borç", techPage);
        Assert.DoesNotContain("Borçlu teslim et", techPage); // seçenek teknisyene görünmez
        await StepAsync(technician, order, "Deliver", new() { ["AllowBalanceDue"] = "true" }); // elle gönderse de olmaz
        Assert.Contains("sadece atölye sahibinin", await BodyAsync(technician, order));

        await StepAsync(owner, order, "Deliver");
        Assert.Contains("Önce tahsilatı kaydet", await BodyAsync(owner, order));

        await StepAsync(owner, order, "Deliver", new() { ["AllowBalanceDue"] = "true" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Cihaz teslim edildi.", page);
        Assert.Contains("Borçlu teslim", page);
    }

    [DockerFact]
    public async Task Only_the_owner_refunds_and_cancel_needs_the_refund_first()
    {
        var owner = await _flows.OwnerAsync("odeme-iade@example.com");
        var technician = await _flows.TechnicianAsync(owner, "odeme-iade-usta@example.com");
        var order = await QuotedAsync(owner, "0532 141 41 41");
        await PayAsync(owner, order, "300", "Cash", "Kapora");

        Assert.DoesNotContain("Para iadesi", await BodyAsync(technician, order));
        await technician.SubmitAsync(order, order + "/refunds", new() { ["Amount"] = "300", ["Method"] = "Cash" });
        Assert.Contains("sadece atölye sahibinin", await BodyAsync(technician, order));

        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "Müşteri vazgeçti" });
        Assert.Contains("İptalden önce alınan ödemeyi iade et.", await BodyAsync(owner, order));

        await owner.SubmitAsync(order, order + "/refunds", new() { ["Amount"] = "300", ["Method"] = "Cash", ["Note"] = "Kapora geri" });
        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "Müşteri vazgeçti" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Kapora geri", page);
        Assert.Contains("stamp stamp-warn is-current", page);
    }

    [DockerFact]
    public async Task Declined_quote_takes_a_diagnosis_fee()
    {
        var owner = await _flows.OwnerAsync("odeme-inceleme@example.com");
        var order = await QuotedAsync(owner, "0532 151 51 51");
        await StepAsync(owner, order, "SendQuote");
        await StepAsync(owner, order, "RecordDecline", new() { ["DecidedBy"] = "Zeynep Arslan" });

        await owner.SubmitAsync(order, order + "/diagnosis-fee", new() { ["fee"] = "150" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("İnceleme ücreti kaydedildi.", page);
        Assert.Contains("150,00", page);

        await PayAsync(owner, order, "150", "Cash");
        await StepAsync(owner, order, "Deliver");
        Assert.Contains("Cihaz teslim edildi.", await BodyAsync(owner, order));
    }

    private static async Task<string> QuotedAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        await StepAsync(owner, order, "StartDiagnosis");
        await owner.SubmitAsync(order, order + "/quote-lines",
            new() { ["Kind"] = "Labor", ["Description"] = "Ekran değişimi", ["Quantity"] = "1", ["UnitPrice"] = "1000", ["VatRate"] = "20" });
        return order;
    }

    private static async Task<string> ReadyAsync(Browser owner, string phone)
    {
        var order = await QuotedAsync(owner, phone);
        await StepAsync(owner, order, "StartRepair");
        await StepAsync(owner, order, "MarkReady");
        return order;
    }

    private static Task<HttpResponseMessage> PayAsync(Browser browser, string order, string amount, string method, string? note = null) =>
        browser.SubmitAsync(order, order + "/payments", new() { ["Amount"] = amount, ["Method"] = method, ["Note"] = note ?? "" });

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step, Dictionary<string, string>? extra = null)
    {
        var fields = extra ?? [];
        fields["Step"] = step;
        return browser.SubmitAsync(order, order + "/steps", fields);
    }
}
