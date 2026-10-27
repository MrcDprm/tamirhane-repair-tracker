using System.Net;

namespace Tamirhane.Web.Tests;

/// <summary>Dil seçimi HTTP üzerinden: çerez, tarayıcı dili, sadece site içine dönüş.</summary>
[Collection(WebCollection.Name)]
public sealed class LanguageFlowTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public async Task Chosen_language_is_remembered_and_wins_over_the_browser_language()
    {
        var browser = app.NewBrowser();

        var response = await browser.SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "en", ["returnUrl"] = "/account/login" });

        Assert.Equal("/account/login", Browser.RedirectOf(response));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-tamirhane-lang="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/login");
        request.Headers.AcceptLanguage.ParseAdd("tr-TR"); // tarayıcı Türkçe ama seçim İngilizce
        var page = await browser.SendAsync(request);
        Assert.Equal("en-GB", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task Without_a_choice_the_browser_language_is_used()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/login");
        request.Headers.AcceptLanguage.ParseAdd("en-US");

        var page = await app.NewBrowser().SendAsync(request);

        Assert.Equal("en-GB", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task Turkish_is_the_default()
    {
        var page = await app.NewBrowser().GetAsync("/account/login");

        Assert.Equal("tr-TR", page.Content.Headers.ContentLanguage.Single());
    }

    [DockerFact]
    public async Task It_never_redirects_outside_the_site()
    {
        var response = await app.NewBrowser().SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "en", ["returnUrl"] = "https://evil.example/phish" });

        Assert.Equal("/", Browser.RedirectOf(response));
    }

    [DockerFact]
    public async Task Sign_in_page_texts_and_errors_follow_the_language()
    {
        var browser = await EnglishAsync(app.NewBrowser(), "/account/login");

        var page = await (await browser.GetAsync("/account/login")).Content.ReadAsStringAsync();
        Assert.Contains("<html lang=\"en\">", page);
        Assert.Contains("<h1>Sign in</h1>", page);
        Assert.Contains(">Türkçe</button>", page); // geri dönüş düğmesi
        Assert.DoesNotContain("Giriş yap", page);

        // Doğrulama mesajı (DataAnnotations) ve denetleyicinin hata metni de İngilizce.
        var empty = await browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = "", ["Password"] = "x" });
        Assert.Contains("Enter your email address.", await empty.Content.ReadAsStringAsync());
        var wrong = await browser.SubmitAsync("/account/login", "/account/login",
            new() { ["Email"] = "yok-boyle-biri@example.com", ["Password"] = "yanlis-sifre-123" });
        Assert.Contains("Wrong email or password.", await wrong.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task The_panel_switches_language_for_this_person_only()
    {
        var flows = new ShopFlows(app);
        var owner = await flows.OwnerAsync("dil-sahibi@example.com");

        var english = await ShopFlows.BodyAsync(await EnglishAsync(owner, "/panel"), "/panel");
        Assert.Contains("<h1>Today</h1>", english);
        Assert.Contains(">Dashboard</a>", english);
        Assert.Contains(">Sign out</button>", english);

        // Başka bir kullanıcı Türkçe görmeye devam eder: seçim kişinin çerezinde, sunucuda ortak bir ayar değil.
        var other = await flows.OwnerAsync("dil-sahibi-2@example.com");
        Assert.Contains("<h1>Bugün</h1>", await ShopFlows.BodyAsync(other, "/panel"));
    }

    /// <summary>Dil düğmesine basar; formun CSRF token'ı verilen sayfadan alınır.</summary>
    private static async Task<Browser> EnglishAsync(Browser browser, string page)
    {
        var response = await browser.SubmitAsync(page, "/language", new() { ["culture"] = "en", ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    [DockerFact]
    public async Task An_unknown_language_changes_nothing()
    {
        var response = await app.NewBrowser().SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "de", ["returnUrl"] = "/account/login" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") &&
                     response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("__Host-tamirhane-lang=")));
    }
}
