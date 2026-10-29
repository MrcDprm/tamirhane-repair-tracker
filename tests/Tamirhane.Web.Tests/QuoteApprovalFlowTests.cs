using System.Net;
using System.Text.RegularExpressions;
using Tamirhane.Web.RateLimiting;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Müşteri teklifi takip sayfasından onaylar ya da reddeder: bir kez, CSRF token'ıyla, IP sınırıyla; atölyeye e-posta gider.</summary>
[Collection(WebCollection.Name)]
public sealed partial class QuoteApprovalFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Customer_sees_the_quote_and_approves_it()
    {
        var owner = await _flows.OwnerAsync("onay@example.com");
        var (order, tracking) = await QuotedAsync(owner, "0532 701 01 01");
        var customer = app.NewBrowser();

        var page = await BodyAsync(customer, tracking);
        foreach (var expected in new[] { "Ekran değişimi", "1.000,00", "1.200,00", "Onarımı onayla", "Onarım istemiyorum" })
            Assert.Contains(expected, page);

        var response = await DecideAsync(customer, tracking, approved: true, "Zeynep Arslan");
        Assert.Equal(tracking, Browser.RedirectOf(response));

        page = await BodyAsync(customer, tracking);
        Assert.Contains("Onayın atölyeye iletildi.", page);
        Assert.Contains("Onaylandı", page);
        Assert.DoesNotContain("Onarımı onayla", page);
        Assert.DoesNotContain("Zeynep Arslan", page); // yazılan ad takip sayfasında görünmez

        var staff = await BodyAsync(owner, order);
        Assert.Contains("Zeynep Arslan, takip sayfasından", staff);
        Assert.Contains("Onarımda", staff);
    }

    [DockerFact]
    public async Task Owner_and_branch_staff_get_an_email_but_staff_of_another_branch_does_not()
    {
        var owner = await _flows.OwnerAsync("onay-eposta@example.com");
        await _flows.TechnicianAsync(owner, "onay-eposta-usta@example.com");
        var elsewhere = await _flows.TechnicianAsync(owner, "onay-eposta-diger@example.com");
        await owner.SubmitAsync("/settings", "/settings/branches", new() { ["NewBranch.Name"] = "Kadıköy" });
        var kadikoy = BranchOption().Match(await BodyAsync(elsewhere, "/panel")).Groups[1].Value;
        await elsewhere.SubmitAsync("/panel", "/branch/switch", new() { ["branchId"] = kadikoy, ["returnUrl"] = "/panel" });
        var (order, tracking) = await QuotedAsync(owner, "0532 702 02 02");

        await DecideAsync(app.NewBrowser(), tracking, approved: false, "Zeynep Arslan");

        var mail = Assert.Single(app.Emails.SentTo("onay-eposta@example.com"), m => m.Subject.Contains("müşteri onarım istemedi"));
        Assert.Matches(@"^\d{4}-\d{4}: müşteri onarım istemedi$", mail.Subject);
        Assert.Contains("Apple iPhone 13", mail.Text);
        Assert.Contains("1.200,00", mail.Text);
        Assert.Contains(order, mail.Text);
        Assert.Single(app.Emails.SentTo("onay-eposta-usta@example.com"), m => m.Subject.Contains("müşteri onarım istemedi"));
        Assert.DoesNotContain(app.Emails.SentTo("onay-eposta-diger@example.com"), m => m.Subject.Contains("müşteri onarım"));
    }

    [DockerFact]
    public async Task Decision_needs_a_name()
    {
        var owner = await _flows.OwnerAsync("onay-ad@example.com");
        var (order, tracking) = await QuotedAsync(owner, "0532 703 03 03");

        var response = await DecideAsync(app.NewBrowser(), tracking, approved: true, "  ");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("adını ve soyadını gir", await response.Content.ReadAsStringAsync());
        Assert.Contains("Onay bekliyor", await BodyAsync(owner, order));
    }

    [DockerFact]
    public async Task Decision_is_given_only_once()
    {
        var owner = await _flows.OwnerAsync("onay-bir-kez@example.com");
        var (order, tracking) = await QuotedAsync(owner, "0532 704 04 04");
        var customer = app.NewBrowser();
        await DecideAsync(customer, tracking, approved: true, "Zeynep Arslan");

        // İkinci sekmede açık kalan form: token geçerli ama karar artık beklenmiyor.
        var again = await customer.SubmitAsync("/account/login", tracking + "/decision",
            new() { ["Approved"] = "false", ["DecidedBy"] = "Zeynep Arslan" });

        Assert.Equal(tracking, Browser.RedirectOf(again));
        Assert.Contains("Bu teklif için karar zaten verilmiş", await BodyAsync(customer, tracking));
        Assert.Contains("Onarımda", await BodyAsync(owner, order));
    }

    [DockerFact]
    public async Task Decision_without_csrf_token_is_rejected()
    {
        var owner = await _flows.OwnerAsync("onay-csrf@example.com");
        var (order, tracking) = await QuotedAsync(owner, "0532 705 05 05");
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var response = await client.PostAsync(tracking + "/decision",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Approved"] = "true", ["DecidedBy"] = "Sahte" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Onay bekliyor", await BodyAsync(owner, order));
    }

    [DockerFact]
    public async Task Draft_quote_is_not_shown_and_cannot_be_decided()
    {
        var owner = await _flows.OwnerAsync("onay-taslak@example.com");
        var order = await ReceiveAsync(owner, "0532 706 06 06");
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        await AddLaborAsync(owner, order);
        var tracking = await TrackingOfAsync(owner, order);
        var customer = app.NewBrowser();

        var page = await BodyAsync(customer, tracking);
        Assert.DoesNotContain("Ekran değişimi", page);
        Assert.DoesNotContain("Onarımı onayla", page);

        // Sayfada form yokken elle gönderilen karar kaydedilmez.
        var forged = await customer.SubmitAsync("/account/login", tracking + "/decision",
            new() { ["Approved"] = "true", ["DecidedBy"] = "Zeynep Arslan" });
        Assert.Equal(tracking, Browser.RedirectOf(forged));
        Assert.Contains("Arıza tespiti", await BodyAsync(owner, order));
    }

    [DockerFact]
    public async Task Unknown_code_gets_404_and_decisions_share_the_ip_limit()
    {
        var customer = app.NewBrowser();
        var unknown = await customer.SubmitAsync("/account/login", "/t/7K3M9QX2HFDA/decision",
            new() { ["Approved"] = "true", ["DecidedBy"] = "Ayşe" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        for (var i = 1; i < TrackingRateLimit.PermitsPerMinute; i++)
            await customer.GetAsync("/t/7K3M9QX2HFDA");
        var refused = await customer.SubmitAsync("/account/login", "/t/7K3M9QX2HFDA/decision",
            new() { ["Approved"] = "true", ["DecidedBy"] = "Ayşe" });

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [DockerFact]
    public async Task Customer_who_switched_to_english_decides_in_english()
    {
        var owner = await _flows.OwnerAsync("onay-en@example.com");
        var (_, tracking) = await QuotedAsync(owner, "0532 709 09 09");
        var customer = app.NewBrowser();
        var english = tracking + "?lang=en";

        var page = await BodyAsync(customer, english);
        Assert.Contains($"action=\"{tracking}/decision?lang=en\"", page); // form seçilen dili taşır
        Assert.Contains(">Approve the repair</button>", page);

        var noName = await customer.SubmitAsync(english, tracking + "/decision?lang=en", new() { ["Approved"] = "true", ["DecidedBy"] = " " });
        Assert.Contains("Enter your full name so we can record who decided", await noName.Content.ReadAsStringAsync());

        var done = await customer.SubmitAsync(english, tracking + "/decision?lang=en", new() { ["Approved"] = "true", ["DecidedBy"] = "Zeynep Arslan" });
        Assert.Equal(english, Browser.RedirectOf(done));
        Assert.Contains("Your approval was sent to the shop.", await BodyAsync(customer, english));
    }

    private static async Task<(string Order, string Tracking)> QuotedAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        await AddLaborAsync(owner, order);
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "SendQuote" });
        return (order, await TrackingOfAsync(owner, order));
    }

    private static Task AddLaborAsync(Browser owner, string order) =>
        owner.SubmitAsync(order, order + "/quote-lines",
            new() { ["Kind"] = "Labor", ["Description"] = "Ekran değişimi", ["Quantity"] = "1", ["UnitPrice"] = "1000", ["VatRate"] = "20" });

    private static async Task<string> TrackingOfAsync(Browser owner, string order) =>
        TrackingLink().Match(await BodyAsync(owner, order + "/receipt")).Groups[1].Value;

    private static Task<HttpResponseMessage> DecideAsync(Browser customer, string tracking, bool approved, string name) =>
        customer.SubmitAsync(tracking, tracking + "/decision", new() { ["Approved"] = approved ? "true" : "false", ["DecidedBy"] = name });

    [GeneratedRegex(@"https?://[^/""]+(/t/[0-9A-Z]{12})")]
    private static partial Regex TrackingLink();

    [GeneratedRegex(@"<option value=""([0-9a-f-]{36})""[^>]*>Kadıköy</option>")]
    private static partial Regex BranchOption();
}
