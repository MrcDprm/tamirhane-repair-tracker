using System.Net;
using Microsoft.AspNetCore.Http;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Tests;

/// <summary>Hesap formlarına IP başına dakikada 10 gönderim; fazlası 429.</summary>
[Collection(WebCollection.Name)]
public sealed class RateLimitTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public async Task Eleventh_form_post_in_a_minute_is_refused_for_that_ip_only()
    {
        var attacker = app.NewBrowser();
        for (var i = 0; i < AccountRateLimit.PermitsPerMinute; i++)
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(attacker, $"deneme{i}@example.com")).StatusCode);

        var refused = await LoginAsync(attacker, "deneme-son@example.com");
        var neighbour = await LoginAsync(app.NewBrowser(), "komsu@example.com");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);
        Assert.Contains("Bir dakika sonra tekrar deneyin", await refused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, neighbour.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await attacker.GetAsync("/account/login")).StatusCode); // sayfayı açmak sayılmaz
    }

    [Fact]
    public void Ip_header_is_ignored_unless_configured()
    {
        var context = Request("203.0.113.7", header: "198.51.100.1");

        Assert.Equal("203.0.113.7", AccountRateLimit.ClientKey(context, clientIpHeader: null));
        Assert.Equal("198.51.100.1", AccountRateLimit.ClientKey(context, "CF-Connecting-IP"));
    }

    [Fact]
    public void Unreadable_ip_header_falls_back_to_the_connection()
    {
        var context = Request("203.0.113.7", header: "198.51.100.1, 10.0.0.1");

        Assert.Equal("203.0.113.7", AccountRateLimit.ClientKey(context, "CF-Connecting-IP"));
    }

    [Fact]
    public void Ipv6_addresses_in_the_same_64_block_share_a_limit()
    {
        var first = AccountRateLimit.ClientKey(Request("2001:db8:1:2:aaaa::1"), null);
        var second = AccountRateLimit.ClientKey(Request("2001:db8:1:2:bbbb::9"), null);
        var otherBlock = AccountRateLimit.ClientKey(Request("2001:db8:1:3::1"), null);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherBlock);
        Assert.Equal("203.0.113.7", AccountRateLimit.ClientKey(Request("::ffff:203.0.113.7"), null));
    }

    private static DefaultHttpContext Request(string remoteIp, string? header = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (header is not null)
            context.Request.Headers["CF-Connecting-IP"] = header;
        return context;
    }

    private static Task<HttpResponseMessage> LoginAsync(Browser browser, string email) =>
        browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = email, ["Password"] = "yanlis-sifre-1" });
}
