using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// OAuth yetkilendirme adresi: OpenIddict isteği (istemci, yönlendirme adresi, PKCE) doğruladıktan sonra buraya geçer.
/// Oturum yoksa normal giriş sayfasına gidilir, dönüşte izin ekranı gösterilir; kullanıcı onaylarsa kod verilir.
/// İzin her bağlantıda yeniden sorulur (izin ekranı atlanmaz): bir istemci kaydı başka biri tarafından yapılmış olabilir.
/// </summary>
[Route(OAuthServer.AuthorizePath)]
public sealed class AuthorizationController(
    IOpenIddictApplicationManager applications, IOpenIddictAuthorizationManager authorizations,
    UserManager<ApplicationUser> users, IShopRepository shops, ICurrentUser current, AccountOptions options) : Controller
{
    [HttpGet, HttpPost]
    public async Task<IActionResult> Authorize(string? decision, CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict isteği yok.");

        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!session.Succeeded)
        {
            // Girişten sonra aynı istekle buraya dönülür (POST'ta da parametreler sorguya taşınır).
            var parameters = Request.HasFormContentType
                ? Request.Form.Where(p => p.Key is not ("__RequestVerificationToken" or "decision")).ToList()
                : Request.Query.ToList();
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters),
            }, IdentityConstants.ApplicationScheme);
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!, ct)
            ?? throw new InvalidOperationException("İstemci bulunamadı (OpenIddict doğrulamış olmalıydı).");
        var appName = await applications.GetLocalizedDisplayNameAsync(application, ct) ?? request.ClientId!;
        var user = await users.GetUserAsync(session.Principal);
        if (user is null || current.ShopId == Guid.Empty)
            return View("~/Views/Mcp/NoShop.cshtml");

        // GET ya da karar yoksa izin ekranı. Karar sadece formdan (POST + CSRF token) gelir.
        if (!HttpMethods.IsPost(Request.Method) || decision is null)
        {
            // Form bu siteye gönderilir ama cevap istemcinin adresine yönlendirir; tarayıcı form-action'ı yönlendirmede de
            // uygular. Sadece bu sayfada, OpenIddict'in kayıtlı adreslerle doğruladığı yönlendirme adresinin kökenine izin.
            if (Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var redirect))
                Response.Headers.ContentSecurityPolicy = Security.SecurityHeaders.ContentSecurityPolicy.Replace(
                    "form-action 'self'", $"form-action 'self' {redirect.GetLeftPart(UriPartial.Authority)}");
            var shop = await shops.GetAsync(current.ShopId, ct);
            return View("~/Views/Mcp/Consent.cshtml", new ConsentModel(
                appName, shop?.Name ?? "", current.IsOwner, RedirectHost(request.RedirectUri),
                Request.HasFormContentType
                    ? Request.Form.Where(p => p.Key is not ("__RequestVerificationToken" or "decision")).ToList()
                    : Request.Query.ToList()));
        }

        if (decision != "allow")
            return Forbid(new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user denied access.",
            }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Token'da sadece hesap ve güvenlik damgası; atölye ve rol her MCP isteğinde yeniden okunur (McpAccessHandler).
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(OAuthServer.StampClaim, user.SecurityStamp);
        identity.SetScopes(OAuthServer.Scope);
        identity.SetResources(OAuthServer.Resource(options.PublicOrigin));
        identity.SetDestinations(_ => [Destinations.AccessToken]);

        // Kalıcı izin kaydı: "Yapay zekâ bağlantıları" sayfasında görünür, oradan iptal edilir (token'ları da düşer).
        var authorization = await authorizations.CreateAsync(
            new ClaimsPrincipal(identity), user.Id.ToString(), (await applications.GetIdAsync(application, ct))!,
            AuthorizationTypes.Permanent, [OAuthServer.Scope], ct);
        identity.SetAuthorizationId(await authorizations.GetIdAsync(authorization, ct));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>İzin ekranında "kod şuraya gidecek" bilgisi: claude.ai, vscode.dev ya da "bu bilgisayar".</summary>
    private static string RedirectHost(string? redirectUri) =>
        Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ? (uri.IsLoopback ? "localhost" : uri.Host) : "";
}

public sealed record ConsentModel(
    string AppName, string ShopName, bool IsOwner, string RedirectHost, IReadOnlyList<KeyValuePair<string, StringValues>> Parameters);
