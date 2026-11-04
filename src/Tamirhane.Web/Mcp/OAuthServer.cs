using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Tamirhane.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// Yapay zekâ istemcilerinin (Claude web/mobil/masaüstü, VS Code...) Tamirhane'ye bağlanması: Tamirhane kendi OAuth 2.1
/// yetkilendirme sunucusudur (OpenIddict) ve aynı uygulamada /mcp adresinde MCP sunucusudur.
/// Akış: istemci /mcp'yi token'sız çağırır → 401 + korunan kaynak metadata'sı (RFC 9728) → yetkilendirme sunucusunun
/// metadata'sı (RFC 8414) → dinamik kayıt (RFC 7591) → kullanıcı girişi + izin ekranı → kod + PKCE → token.
/// </summary>
public static class OAuthServer
{
    public const string Scope = "mcp";
    public const string McpPath = "/mcp";
    public const string AuthorizePath = "connect/authorize";
    public const string TokenPath = "connect/token";
    public const string RegisterPath = "connect/register";
    /// <summary>Token'a yazılan güvenlik damgası: şifre değişince ya da oturumlar kapatılınca token da geçersizleşir.</summary>
    public const string StampClaim = "tamirhane:stamp";

    /// <summary>MCP isteklerini doğrulayan şema (<see cref="McpAccessHandler"/>).</summary>
    public const string McpScheme = "tamirhane-mcp";

    /// <summary>Programların çağırdığı adresler (tarayıcı sayfası değil): MCP, token, kayıt ve metadata.</summary>
    public static bool IsApiPath(PathString path) =>
        path.StartsWithSegments(McpPath) || path.StartsWithSegments("/" + TokenPath) || path.StartsWithSegments("/" + RegisterPath)
        || path.StartsWithSegments("/.well-known");

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>MCP adresi; token'ın audience'ı (RFC 8707) ve korunan kaynağın kimliği.</summary>
    public static string Resource(Uri origin) => new Uri(origin, McpPath).AbsoluteUri;

    public static IServiceCollection AddMcpAccess(this IServiceCollection services, Uri origin)
    {
        var resource = Resource(origin);

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<TamirhaneDbContext>())
            .AddServer(options =>
            {
                // Issuer sabit: Host başlığından türetilseydi sahte Host ile başka adres yazdırılabilirdi.
                options.SetIssuer(origin);
                options.SetAuthorizationEndpointUris(AuthorizePath).SetTokenEndpointUris(TokenPath);
                options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
                // OAuth 2.1: PKCE zorunlu, sadece S256 (düz "plain" kabul edilmez).
                options.RequireProofKeyForCodeExchange();
                options.Configure(o => o.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                options.RegisterScopes(Scope);
                // RFC 8707: istemci (claude.ai) token'ı hangi kaynak için istediğini söyler; sadece /mcp kabul edilir.
                options.RegisterResources(resource);
                options.SetAccessTokenLifetime(AccessTokenLifetime).SetRefreshTokenLifetime(RefreshTokenLifetime);
                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5));

                // Token'lar Data Protection ile (uygulamanın mevcut anahtarları, yayında sunucunun diskinde). İmza ve
                // şifreleme anahtarı OpenIddict'in kendisi için gerekli ama JWT üretilmediği için bellekteki geçici
                // anahtar yeterli; yeniden başlayınca token'lar geçersiz olmaz.
                options.UseDataProtection();
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough() // giriş + izin ekranı bizim controller'ımızda
                    .EnableStatusCodePagesIntegration();

                // Dinamik istemci kaydının adresi metadata'da (OpenIddict kaydı kendisi yapmıyor, ClientRegistration yapar).
                options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler =>
                    handler.UseInlineHandler(context =>
                    {
                        context.Metadata["registration_endpoint"] = new Uri(origin, RegisterPath).AbsoluteUri;
                        return default;
                    }));
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseDataProtection();
                options.UseAspNetCore();
                // Başka bir kaynak için verilmiş token burada geçmez.
                options.AddAudiences(resource);
                // Token'ın veritabanındaki kaydı her istekte kontrol edilir: "bağlantıyı kes" hemen etkili olur.
                options.EnableTokenEntryValidation();
                options.EnableAuthorizationEntryValidation();
            });

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, McpAccessHandler>(McpScheme, options =>
                // Token yoksa ya da geçersizse 401 + WWW-Authenticate: resource_metadata (MCP SDK'sının şeması yazar).
                options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme)
            .AddMcp(options => options.ResourceMetadata = new()
            {
                Resource = resource,
                AuthorizationServers = { origin.AbsoluteUri.TrimEnd('/') },
                ScopesSupported = [Scope],
                ResourceName = "Tamirhane",
            });

        services.AddHostedService<OAuthMaintenance>();
        return services;
    }

    /// <summary>OpenIddict'in doğrulama şeması (token'ın kendisini doğrular; <see cref="McpAccessHandler"/> bunu kullanır).</summary>
    public const string TokenScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
}
