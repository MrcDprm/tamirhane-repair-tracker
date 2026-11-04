using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Tamirhane.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// MCP isteğinin kimliği. Token sadece "hangi hesap" ve güvenlik damgasını taşır; atölye, şube, rol ve ad her istekte
/// veritabanından, oturum çerezindekiyle aynı fabrikayla yeniden üretilir. Böylece personel çıkarılırsa, rolü
/// değişirse ya da şifresi değişirse token'ın yetkisi bir sonraki istekte düşer (çerezde de güvenlik damgası her istekte
/// doğrulanıyor). Mevcut ICurrentUser ve kiracı filtresi MCP'de değişmeden çalışır.
/// Oturum çerezi bu şemada hiç okunmaz: başka bir sitenin tarayıcıdaki çerezle /mcp'ye istek atması işe yaramaz.
/// </summary>
public sealed class McpAccessHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = await Context.AuthenticateAsync(OAuthServer.TokenScheme);
        if (!token.Succeeded)
            return token.None ? AuthenticateResult.NoResult() : AuthenticateResult.Fail("invalid token");
        if (!token.Principal.HasScope(OAuthServer.Scope))
            return AuthenticateResult.Fail("missing scope");

        var user = await users.FindByIdAsync(token.Principal.GetClaim(Claims.Subject) ?? "");
        if (user is null || !user.EmailConfirmed || await users.IsLockedOutAsync(user)
            || user.SecurityStamp != token.Principal.GetClaim(OAuthServer.StampClaim))
            return AuthenticateResult.Fail("revoked");

        var principal = await signIn.CreateUserPrincipalAsync(user);
        if (!principal.HasClaim(c => c.Type == ShopClaims.ShopId))
            return AuthenticateResult.Fail("no shop");
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
