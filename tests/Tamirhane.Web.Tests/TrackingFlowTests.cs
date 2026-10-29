using System.Net;
using System.Text.RegularExpressions;
using Tamirhane.Web.RateLimiting;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Müşteri takip sayfası (/t/kod): girişsiz erişim, gizlilik, 404, rate limit ve atölyedeki değişikliklerin yansıması; HTTP üzerinden.</summary>
[Collection(WebCollection.Name)]
public sealed partial class TrackingFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Customer_sees_device_status_and_shop_contact_without_signing_in()
    {
        var owner = await _flows.OwnerAsync("takip@example.com");
        await SetBranchAsync(owner, "Moda Cd. No: 12 Kadıköy", "0216 345 67 89");
        var (_, tracking) = await ReceiveAsync(owner, "0532 301 01 01");

        var response = await app.NewBrowser().GetAsync(tracking);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Razor "+" işaretini &#x2B; diye kodlar; tarayıcı linki yine +90... olarak açar.
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        foreach (var expected in new[] { "Apple iPhone 13", "Ekran kırık", "Ze**** Ar****", "Teslim alındı", "Arıza tespiti",
                     "Kaya Telefon", "Moda Cd. No: 12 Kadıköy", "href=\"tel:+902163456789\"", "0216 345 67 89", "Yol tarifi" }.Where(e => !page.Contains(e)))
            Assert.Fail($"Sayfada yok: {expected}");
        Assert.DoesNotContain("class=\"shell\"", page); // atölye menüsü yok
    }

    [DockerFact]
    public async Task Page_never_contains_full_name_phone_email_passcode_or_imei()
    {
        var owner = await _flows.OwnerAsync("takip-gizli@example.com");
        var (_, tracking) = await ReceiveAsync(owner, "0532 302 02 02");

        var page = await BodyAsync(app.NewBrowser(), tracking);

        foreach (var secret in new[] { "Zeynep", "Arslan", "0532 302", "+90532302", "zeynep@example.com", "4826", "490154203237518", "Ayşe Kaya" })
            Assert.DoesNotContain(secret, page);
    }

    [DockerFact]
    public async Task Page_is_kept_out_of_search_engines_referrers_and_caches()
    {
        var owner = await _flows.OwnerAsync("takip-baslik@example.com");
        var (_, tracking) = await ReceiveAsync(owner, "0532 303 03 03");

        var response = await app.NewBrowser().GetAsync(tracking);

        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [DockerFact]
    public async Task Invalid_and_unknown_codes_get_the_same_404()
    {
        var visitor = app.NewBrowser();

        var invalid = await visitor.GetAsync("/t/yok");
        var unknown = await visitor.GetAsync("/t/7K3M9QX2HFDA");

        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(await invalid.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Contains("Bu takip kodu bulunamadı", await unknown.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Code_works_in_lower_case_and_with_dashes()
    {
        var owner = await _flows.OwnerAsync("takip-yazim@example.com");
        var (_, tracking) = await ReceiveAsync(owner, "0532 304 04 04");
        var code = tracking[3..];

        var typed = $"/t/{code[..4].ToLowerInvariant()}-{code[4..8]}-{code[8..]}";
        Assert.Equal(HttpStatusCode.OK, (await app.NewBrowser().GetAsync(typed)).StatusCode);
    }

    [DockerFact]
    public async Task Workshop_changes_status_notes_and_promise_show_up_but_private_notes_do_not()
    {
        var owner = await _flows.OwnerAsync("takip-guncel@example.com");
        var (order, tracking) = await ReceiveAsync(owner, "0532 305 05 05");
        var promised = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(4);

        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "Ekran siparişi verildi", ["VisibleToCustomer"] = "true" });
        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "Kasada darbe izi var" });
        Assert.Equal(order, Browser.RedirectOf(await owner.SubmitAsync(order, order + "/promise", new() { ["Date"] = promised.ToString("yyyy-MM-dd") })));

        var page = await BodyAsync(app.NewBrowser(), tracking);
        Assert.Matches(@"class=""is-current"" aria-current=""step"">\s*<span class=""track-dot""[^>]*></span>\s*<span class=""track-step"">Arıza tespiti", page);
        Assert.Contains("Ekran siparişi verildi", page);
        Assert.DoesNotContain("Kasada darbe izi var", page);
        Assert.Contains("Tahmini teslim", page);
        Assert.Contains(promised.ToString("d MMMM dddd", new System.Globalization.CultureInfo("tr-TR")), page);
    }

    [DockerFact]
    public async Task Too_many_requests_from_one_address_are_refused()
    {
        var visitor = app.NewBrowser();
        for (var i = 0; i < TrackingRateLimit.PermitsPerMinute; i++)
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/t/7K3M9QX2HFD" + (i % 10))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await visitor.GetAsync("/t/7K3M9QX2HFDA")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await app.NewBrowser().GetAsync("/t/7K3M9QX2HFDA")).StatusCode); // başka adres etkilenmez
    }

    [DockerFact]
    public async Task Page_opens_in_the_shop_customer_language_and_the_customer_can_switch()
    {
        var owner = await _flows.OwnerAsync("takip-dil@example.com");
        var (_, tracking) = await ReceiveAsync(owner, "0532 304 04 04");

        // Personelin panel dili İngilizce olsa da müşterinin gördüğü sayfa atölyenin müşteri dilinde (Türkçe).
        await owner.SubmitAsync("/panel", "/language", new() { ["culture"] = "en", ["returnUrl"] = "/" });
        var page = await BodyAsync(owner, tracking);
        Assert.Contains("<html lang=\"tr\">", page);
        Assert.Contains(">Onarımın adımları</h2>", page);
        Assert.Contains($"href=\"{tracking}?lang=en\"", page);

        // Geçiş linki sayfayı ve düzeni (altbilgi) birlikte çevirir.
        var english = await BodyAsync(app.NewBrowser(), tracking + "?lang=en");
        foreach (var expected in new[] { "<html lang=\"en\">", ">Repair steps</h2>", ">Checked in<", "This page is just for you.", $"href=\"{tracking}?lang=tr\"" })
            Assert.Contains(expected, english);
        Assert.DoesNotContain("Teslim alındı", english);
    }

    [DockerFact]
    public async Task Shop_that_writes_to_customers_in_english_gets_an_english_page()
    {
        var owner = await _flows.OwnerAsync("takip-en@example.com");
        Assert.Equal("/settings", Browser.RedirectOf(await owner.SubmitAsync("/settings", "/settings", new()
        {
            ["Shop.Name"] = "Kaya Telefon", ["Shop.WarrantyDays"] = "90", ["Shop.DefaultVatRate"] = "20", ["Shop.Locale"] = "en",
        })));
        var (_, tracking) = await ReceiveAsync(owner, "0532 305 05 05");

        var page = await BodyAsync(app.NewBrowser(), tracking);
        Assert.Contains("<html lang=\"en\">", page);
        Assert.Contains("Your device is at the shop.", page);
    }

    private static async Task<(string Order, string Tracking)> ReceiveAsync(Browser owner, string phone)
    {
        var response = await owner.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.CustomerEmail"] = "zeynep@example.com",
            ["Input.KvkkConsent"] = "true", ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Imei"] = "490154203237518", ["Input.Complaint"] = "Ekran kırık", ["Input.Passcode"] = "4826",
        });
        var order = Browser.RedirectOf(response);
        var tracking = TrackingLink().Match(await BodyAsync(owner, order + "/receipt")).Groups[1].Value;
        return (order, tracking);
    }

    private static async Task SetBranchAsync(Browser owner, string address, string phone)
    {
        var settings = await BodyAsync(owner, "/settings");
        var action = BranchForm().Match(settings).Groups[1].Value;
        Assert.Equal("/settings", Browser.RedirectOf(await owner.SubmitAsync("/settings", action, new()
        {
            ["Name"] = "Merkez", ["Address"] = address, ["Phone"] = phone,
        })));
    }

    [GeneratedRegex(@"https?://[^/""]+(/t/[0-9A-Z]{12})")]
    private static partial Regex TrackingLink();

    [GeneratedRegex(@"action=""(/settings/branches/[0-9a-f-]{36})""")]
    private static partial Regex BranchForm();
}
