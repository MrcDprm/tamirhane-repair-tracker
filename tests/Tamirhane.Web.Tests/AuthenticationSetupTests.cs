using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tamirhane.Application.Abstractions;

namespace Tamirhane.Web.Tests;

/// <summary>Uygulamanın gerçek kayıtlarında oturum çerezi güvenlik listesine uyuyor.</summary>
[Collection(WebCollection.Name)]
public sealed class AuthenticationSetupTests(TamirhaneWebFactory app)
{
    [DockerFact]
    public void Session_cookie_is_host_only_http_only_secure_and_lax()
    {
        var cookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme).Cookie;

        Assert.Equal("__Host-tamirhane", cookie.Name);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path);
        Assert.Null(cookie.Domain);
    }

    [DockerFact]
    public void Security_stamp_is_checked_on_every_request()
    {
        var options = app.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;

        Assert.Equal(TimeSpan.Zero, options.ValidationInterval);
    }

    [DockerFact]
    public void Antiforgery_cookie_is_also_host_only_and_secure()
    {
        var cookie = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie;

        Assert.StartsWith("__Host-", cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
    }

    [DockerFact]
    public void Current_user_comes_from_the_session()
    {
        using var scope = app.Services.CreateScope();

        Assert.Equal("HttpCurrentUser", scope.ServiceProvider.GetRequiredService<ICurrentUser>().GetType().Name);
    }
}
