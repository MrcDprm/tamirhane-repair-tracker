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
    public async Task An_unknown_language_changes_nothing()
    {
        var response = await app.NewBrowser().SubmitAsync("/account/login", "/language",
            new() { ["culture"] = "de", ["returnUrl"] = "/account/login" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie") &&
                     response.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("__Host-tamirhane-lang=")));
    }
}
