using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.Controllers;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Sahip politikası tek başına: senaryolardaki sahip kontrolü ikinci katman, bu testler o katman olmadan da
/// teknisyenin sahip sayfalarına giremediğini gösterir. Docker gerekmez.
/// </summary>
public sealed class AuthorizationPolicyTests
{
    /// <summary>Sadece sahibin açabileceği sayfalar. Ciro sayfası (#33) gelince buraya eklenir.</summary>
    private static readonly Type[] OwnerControllers = [typeof(StaffController), typeof(SettingsController)];

    [Fact]
    public void Owner_pages_require_the_owner_policy()
    {
        foreach (var controller in OwnerControllers)
            Assert.Equal(Policies.OwnerOnly, controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
    }

    [Fact]
    public void Owner_pages_have_no_anonymous_action()
    {
        var anonymous = OwnerControllers.SelectMany(c => c.GetMethods())
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        Assert.Empty(anonymous);
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Technician", false)]
    [InlineData(null, false)]
    public async Task Only_the_owner_role_passes(string? role, bool allowed)
    {
        var services = new ServiceCollection().AddLogging().AddShopPolicies().BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity("test");
        if (role is not null)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        var result = await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), Policies.OwnerOnly);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task Signed_out_visitor_does_not_pass()
    {
        var services = new ServiceCollection().AddLogging().AddShopPolicies().BuildServiceProvider();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity()); // kimlik doğrulanmamış

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(anonymous, Policies.OwnerOnly);

        Assert.False(result.Succeeded);
    }
}
