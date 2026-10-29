using System.Net;
using System.Text.RegularExpressions;
using Tamirhane.Web.RateLimiting;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>"Demoyu dene" HTTP üzerinden: kendi atölyesi, örnek veri, ayrım, e-posta yok, çıkış ve IP sınırı.</summary>
[Collection(WebCollection.Name)]
public sealed partial class DemoFlowTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public async Task Visitor_gets_a_sample_workshop_and_lands_on_the_panel_as_its_owner()
    {
        var visitor = await DemoAsync();

        var panel = await BodyAsync(visitor, "/panel");
        Assert.Contains("Bu bir demo atölye", panel);
        Assert.Contains("Demo hesabı", panel);
        Assert.DoesNotContain(".invalid", panel); // demo hesabının adresi gösterilmez

        var list = await BodyAsync(visitor, "/work-orders?filter=All");
        foreach (var expected in new[] { "Asus VivoBook 15", "PlayStation 5", "MacBook Air M1", "Onay bekliyor", "Teslime hazır", "İptal" })
            Assert.Contains(expected, list);
        Assert.Contains("IP13-SCR", await BodyAsync(visitor, "/inventory"));
        Assert.Contains("Mert Demir", await BodyAsync(visitor, "/staff"));
    }

    [DockerFact]
    public async Task Each_visitor_has_a_separate_demo()
    {
        var first = await DemoAsync();
        var second = await DemoAsync();

        Assert.Equal("/settings", Browser.RedirectOf(await first.SubmitAsync("/settings", "/settings", new()
        {
            ["Shop.Name"] = "Benim Denemem", ["Shop.WarrantyDays"] = "90", ["Shop.DefaultVatRate"] = "20", ["Shop.Locale"] = "tr",
        })));

        Assert.Contains("Benim Denemem", await BodyAsync(first, "/settings"));
        Assert.DoesNotContain("Benim Denemem", await BodyAsync(second, "/settings"));
    }

    [DockerFact]
    public async Task New_intake_in_a_demo_continues_the_numbering()
    {
        var visitor = await DemoAsync();
        var before = NumberPattern().Matches(await BodyAsync(visitor, "/work-orders?filter=All")).Select(m => m.Value).ToHashSet();

        var order = await ReceiveAsync(visitor, "0533 444 55 66");

        var number = NumberPattern().Match(await BodyAsync(visitor, order)).Value;
        Assert.DoesNotContain(number, before);
    }

    [DockerFact]
    public async Task Customer_side_of_a_sample_order_works_too()
    {
        var visitor = await DemoAsync();
        var awaiting = WorkOrderLink().Match(await BodyAsync(visitor, "/work-orders?filter=AwaitingApproval")).Value;

        var tracking = TrackingLink().Match(await BodyAsync(visitor, awaiting + "/receipt")).Groups[1].Value;
        var page = await BodyAsync(app.NewBrowser(), tracking);

        Assert.Contains("Onarımı onayla", page);
    }

    [DockerFact]
    public async Task Demo_sends_no_invitation_email()
    {
        var visitor = await DemoAsync();
        var before = app.Emails.Count;

        var page = await visitor.SubmitAsync("/staff", "/staff/invite", new() { ["Invite.Email"] = "gercek-kisi@example.com" });

        Assert.Contains("Demo atölye e-posta göndermez", await page.Content.ReadAsStringAsync());
        Assert.Empty(app.Emails.SentTo("gercek-kisi@example.com"));
        Assert.Equal(before, app.Emails.Count);
    }

    [DockerFact]
    public async Task Open_your_own_shop_leaves_the_demo_for_the_sign_up_form()
    {
        var visitor = await DemoAsync();

        var response = await visitor.SubmitAsync("/panel", "/demo/leave", new());

        Assert.Equal("/account/register", Browser.RedirectOf(response));
        Assert.StartsWith("/account/login", Browser.RedirectOf(await visitor.GetAsync("/panel")));
    }

    [DockerFact]
    public async Task Demo_opens_in_the_visitors_language()
    {
        var visitor = app.NewBrowser();
        await visitor.SubmitAsync("/", "/language", new() { ["culture"] = "en", ["returnUrl"] = "/" });

        Assert.Equal("/panel", Browser.RedirectOf(await visitor.SubmitAsync("/", "/demo", new())));

        Assert.Contains("This is a demo workshop", await BodyAsync(visitor, "/panel"));
        Assert.Contains("Sample Repair Shop", await BodyAsync(visitor, "/settings"));
    }

    [DockerFact]
    public async Task Too_many_demos_from_one_address_are_refused()
    {
        var visitor = app.NewBrowser();
        for (var i = 0; i < DemoRateLimit.PermitsPerHour; i++)
            Assert.Equal("/panel", Browser.RedirectOf(await visitor.SubmitAsync("/", "/demo", new())));

        var refused = await visitor.SubmitAsync("/", "/demo", new());

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("/panel", Browser.RedirectOf(await app.NewBrowser().SubmitAsync("/", "/demo", new()))); // başka adres etkilenmez
    }

    private async Task<Browser> DemoAsync()
    {
        var visitor = app.NewBrowser();
        Assert.Equal("/panel", Browser.RedirectOf(await visitor.SubmitAsync("/", "/demo", new())));
        return visitor;
    }

    [GeneratedRegex(@"\b\d{4}-\d{4}\b")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"/work-orders/[0-9a-f-]{36}")]
    private static partial Regex WorkOrderLink();

    [GeneratedRegex(@"https?://[^/""]+(/t/[0-9A-Z]{12})")]
    private static partial Regex TrackingLink();
}
