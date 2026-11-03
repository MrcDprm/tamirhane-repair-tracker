using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Web.RateLimiting;
using Tamirhane.Web.Security;

namespace Tamirhane.Web.Tests;

/// <summary>Güvenlik başlıkları, CSRF regresyonu, genel istek sınırı ve hata sayfaları (#38).</summary>
[Collection(WebCollection.Name)]
public sealed partial class SecurityTests(TamirhaneWebFactory app)
{
    [DockerTheory]
    [InlineData("/")]
    [InlineData("/account/login")]
    [InlineData("/css/site.css")]   // statik dosya da
    [InlineData("/yok-boyle-bir-sayfa")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        var response = await app.NewBrowser().GetAsync(path);

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Single(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Single(response, "Referrer-Policy"));
        Assert.Equal(SecurityHeaders.PermissionsPolicy, Single(response, "Permissions-Policy"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public void Policy_allows_only_this_site_and_no_inline_code()
    {
        var csp = SecurityHeaders.ContentSecurityPolicy;

        Assert.Contains("script-src 'self';", csp);
        Assert.Contains("style-src 'self';", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("require-trusted-types-for 'script'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.DoesNotContain("*", csp);
    }

    /// <summary>
    /// Uygulamadaki her POST ucu CSRF token'ı olmadan reddedilir. Yeni bir controller'da genel filtre kaldırılır ya da
    /// [IgnoreAntiforgeryToken] eklenirse bu test düşer. Hesap formlarının dakikada 10 sınırına takılmamak için
    /// istekler üç ayrı sahibe (üç ayrı IP) dağıtılır.
    /// </summary>
    [DockerFact]
    public async Task Every_post_endpoint_rejects_a_request_without_a_csrf_token()
    {
        var flows = new ShopFlows(app);
        Browser[] owners =
        [
            await flows.OwnerAsync("csrf-1@example.com"),
            await flows.OwnerAsync("csrf-2@example.com"),
            await flows.OwnerAsync("csrf-3@example.com"),
        ];
        var posts = PostRoutes().ToList();
        Assert.True(posts.Count > 20, $"Sadece {posts.Count} POST ucu bulundu; uç listesi okunamıyor olabilir.");

        var accepted = new List<string>();
        for (var i = 0; i < posts.Count; i++)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["x"] = "1" });
            var response = await owners[i % owners.Length].PostAsync(posts[i], content);
            if (response.StatusCode != HttpStatusCode.BadRequest)
                accepted.Add($"{posts[i]} → {(int)response.StatusCode}");
        }

        Assert.Empty(accepted);
    }

    [DockerFact]
    public async Task Too_many_requests_from_one_address_are_refused_in_the_page_language()
    {
        var bot = app.NewBrowser();
        for (var i = 0; i < GlobalRateLimit.PermitsPerMinute; i++)
            await bot.GetAsync("/privacy");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/privacy");
        request.Headers.AcceptLanguage.ParseAdd("en-US");
        var refused = await bot.SendAsync(request);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);
        Assert.Contains("Too many attempts", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await app.NewBrowser().GetAsync("/privacy")).StatusCode); // başka adres etkilenmez
        Assert.Equal(HttpStatusCode.OK, (await bot.GetAsync("/health")).StatusCode);               // sağlık kontrolü sayılmaz
    }

    [DockerFact]
    public async Task Unknown_address_shows_the_site_not_found_page()
    {
        var response = await app.NewBrowser().GetAsync("/yok-boyle-bir-sayfa");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("<h1>Sayfa bulunamadı</h1>", html);
        Assert.Contains("href=\"/\"", html);
        Assert.DoesNotContain("İstek numarası", html);
    }

    /// <summary>Uygulamanın POST uçları; adresteki parametreler örnek değerlerle doldurulur.</summary>
    private IEnumerable<string> PostRoutes() =>
        app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("POST") == true)
            .Select(e => "/" + Parameter().Replace(e.RoutePattern.RawText!.TrimStart('/'), m =>
                m.Groups["constraint"].Value.Contains("guid") ? Guid.NewGuid().ToString()
                : m.Groups["constraint"].Value.Contains("int") ? "1"
                : "ABCDEFGHJKLM"))
            .Distinct();

    private static string Single(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? Assert.Single(values)
            : throw new Xunit.Sdk.XunitException($"{name} başlığı yok");

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(?<constraint>:[^}?]*)?\??\}")]
    private static partial Regex Parameter();
}
