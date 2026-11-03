using System.Net;

namespace Tamirhane.Web.Tests;

/// <summary>Tanıtım, gizlilik/KVKK ve kullanım koşulları sayfaları (#37): oturumsuz açılır, iki dilde, alt bilgide yasal linkler.</summary>
[Collection(WebCollection.Name)]
public sealed class PublicPagesTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public async Task Visitor_sees_the_landing_page_with_demo_sign_up_and_legal_links()
    {
        var response = await app.NewBrowser().GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("action=\"/demo\"", html);
        Assert.Contains("href=\"/account/register\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/privacy\"", html);
        Assert.Contains("href=\"/terms\"", html);
        Assert.Contains($"<span data-year>{DateTime.UtcNow.Year}</span> Miraç Deprem", html);
    }

    [DockerFact]
    public async Task Signed_in_staff_go_straight_to_the_panel()
    {
        var owner = await new ShopFlows(app).OwnerAsync("landing@example.com");

        Assert.Equal("/panel", Browser.RedirectOf(await owner.GetAsync("/")));
    }

    [DockerTheory]
    [InlineData("/privacy", "Gizlilik ve KVKK aydınlatma metni", "veri işleyen")]
    [InlineData("/en/privacy", "Privacy and KVKK notice", "data processor")]
    [InlineData("/terms", "Kullanım koşulları", "olduğu gibi")]
    [InlineData("/en/terms", "Terms of use", "as is")]
    public async Task Legal_pages_open_without_a_session_in_both_languages(string path, string heading, string body)
    {
        var response = await app.NewBrowser().GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<h1>{heading}</h1>", html);
        Assert.Contains(body, html);
    }

    [DockerFact]
    public async Task Sign_up_form_points_to_the_terms_and_the_privacy_notice()
    {
        var html = await (await app.NewBrowser().GetAsync("/account/register")).Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"/terms\">kullanım koşullarını</a>", html);
        Assert.Contains("<a href=\"/privacy\">gizlilik metninde</a>", html);
    }
}
