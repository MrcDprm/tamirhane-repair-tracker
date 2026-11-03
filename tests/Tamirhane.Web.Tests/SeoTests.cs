using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Tamirhane.Web.Tests;

/// <summary>Arama motoru (#42): iki dilin adresleri, canonical/hreflang, paylaşım kartı, yapılandırılmış veri, robots ve sitemap.</summary>
[Collection(WebCollection.Name)]
public sealed partial class SeoTests(TamirhaneWebFactory app)
{
    private const string Origin = "https://localhost";

    [DockerTheory]
    [InlineData("/", "tr", "Tamirhane: Ücretsiz teknik servis ve tamir takip programı")]
    [InlineData("/en/", "en", "Tamirhane: Free repair shop software and repair tracking")]
    public async Task Landing_page_has_its_title_canonical_language_alternates_and_card(string path, string lang, string title)
    {
        var html = await (await app.NewBrowser().GetAsync(path)).Content.ReadAsStringAsync();

        Assert.Contains($"<html lang=\"{lang}\">", html);
        Assert.Contains($"<title>{title}</title>", html);
        Assert.Contains($"<link rel=\"canonical\" href=\"{Origin}{path}\" />", html);
        Assert.Contains($"<link rel=\"alternate\" hreflang=\"tr\" href=\"{Origin}/\" />", html);
        Assert.Contains($"<link rel=\"alternate\" hreflang=\"en\" href=\"{Origin}/en/\" />", html);
        Assert.Contains($"<link rel=\"alternate\" hreflang=\"x-default\" href=\"{Origin}/\" />", html);
        Assert.Contains($"<meta property=\"og:image\" content=\"{Origin}/img/og-{lang}.png\" />", html);
        Assert.Matches("<meta name=\"description\" content=\"[^\"]{80,170}\" />", html); // arama sonucunda kesilmeyecek uzunlukta
        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.DoesNotContain("noindex", html);

        using var data = JsonDocument.Parse(JsonLd().Match(html).Groups[1].Value);
        Assert.Equal("SoftwareApplication", data.RootElement.GetProperty("@type").GetString());
        Assert.Equal("0", data.RootElement.GetProperty("offers").GetProperty("price").GetString());
    }

    [DockerTheory]
    [InlineData("/en/privacy", "/privacy", "/en/privacy")]
    [InlineData("/terms", "/terms", "/en/terms")]
    public async Task Legal_pages_point_to_both_languages(string path, string turkish, string english)
    {
        var html = await (await app.NewBrowser().GetAsync(path)).Content.ReadAsStringAsync();

        Assert.Contains($"<link rel=\"canonical\" href=\"{Origin}{path}\" />", html);
        Assert.Contains($"hreflang=\"tr\" href=\"{Origin}{turkish}\"", html);
        Assert.Contains($"hreflang=\"en\" href=\"{Origin}{english}\"", html);
    }

    [DockerTheory]
    [InlineData("/account/login")]
    [InlineData("/en/account/register")]
    [InlineData("/yok-boyle-bir-sayfa")]
    public async Task Account_and_error_pages_stay_out_of_search_results(string path)
    {
        var html = await (await app.NewBrowser().GetAsync(path)).Content.ReadAsStringAsync();

        Assert.Contains("<meta name=\"robots\" content=\"noindex\" />", html);
    }

    [DockerFact]
    public async Task Robots_blocks_the_app_and_points_to_the_sitemap()
    {
        var response = await app.NewBrowser().GetAsync("/robots.txt");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        foreach (var path in new[] { "/panel", "/en/panel", "/work-orders", "/t/", "/en/account/" })
            Assert.Contains($"Disallow: {path}\n", text);
        Assert.DoesNotContain("Disallow: /\n", text); // tanıtım taranır
        Assert.Contains($"Sitemap: {Origin}/sitemap.xml", text);
    }

    [DockerFact]
    public async Task Sitemap_lists_public_pages_in_both_languages_with_alternates()
    {
        var response = await app.NewBrowser().GetAsync("/sitemap.xml");
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var locations = xml.Descendants(sm + "loc").Select(l => l.Value).ToList();
        Assert.Equal([$"{Origin}/", $"{Origin}/en/", $"{Origin}/privacy", $"{Origin}/en/privacy", $"{Origin}/terms", $"{Origin}/en/terms"], locations);
        Assert.All(xml.Descendants(sm + "url"), url => Assert.Equal(3, url.Elements().Count(e => e.Name.LocalName == "link")));
    }

    // Razor türdeki "+" işaretini HTML varlığı olarak yazar (&#x2B;); tarayıcı ve Google ikisini aynı okur.
    [GeneratedRegex("<script type=\"application/ld(?:\\+|&#x2B;)json\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex JsonLd();
}
