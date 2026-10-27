using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Müşteriye durum e-postası (#30) uygulamanın tamamında: her adımda bir e-posta, link takip sayfasını açar,
/// e-posta servisi çökükken iş emri yine ilerler.</summary>
[Collection(WebCollection.Name)]
public sealed partial class CustomerNotificationFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Customer_gets_an_email_at_every_step_with_a_working_tracking_link()
    {
        var owner = await _flows.OwnerAsync("bildirim-akis@example.com");
        var order = await ReceiveWithEmailAsync(owner, "0532 611 01 01", "musteri-akis@example.com");

        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady" })
            await StepAsync(owner, order, step);

        var mails = app.Emails.SentTo("musteri-akis@example.com");
        Assert.Equal(
            ["Apple iPhone 13: teslim alındı", "Apple iPhone 13: inceleniyor", "Apple iPhone 13: onarılıyor", "Apple iPhone 13: teslime hazır"],
            mails.Select(m => m.Subject));

        var link = new Uri(TrackingLink().Match(mails[^1].Text).Value).PathAndQuery;
        var page = await app.NewBrowser().GetAsync(link); // oturumsuz, başka bir tarayıcıdan
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Teslime hazır", await page.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Work_order_moves_on_even_when_the_email_service_is_down()
    {
        var owner = await _flows.OwnerAsync("bildirim-hata@example.com");
        var order = await ReceiveWithEmailAsync(owner, "0532 611 02 02", "musteri-hata@example.com");

        app.Emails.Failing = true;
        try
        {
            Assert.Equal(order, Browser.RedirectOf(await StepAsync(owner, order, "StartDiagnosis")));
            Assert.Contains(">Arıza tespiti<", await BodyAsync(owner, order));
            _ = app.Emails.Count; // kuyruk boşalsın: gönderim denemeleri bitti, hata işi bozmadı
        }
        finally
        {
            app.Emails.Failing = false;
        }

        Assert.DoesNotContain(app.Emails.SentTo("musteri-hata@example.com"), m => m.Subject.EndsWith("inceleniyor"));
    }

    private static async Task<string> ReceiveWithEmailAsync(Browser owner, string phone, string email)
    {
        var response = await owner.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.CustomerEmail"] = email,
            ["Input.KvkkConsent"] = "true", ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kırık",
        });
        return Browser.RedirectOf(response);
    }

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step) =>
        browser.SubmitAsync(order, order + "/steps", new() { ["Step"] = step });

    [GeneratedRegex(@"https?://\S+/t/\S+")]
    private static partial Regex TrackingLink();
}
