using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using Tamirhane.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// Dinamik istemci kaydı (RFC 7591). claude.ai gibi istemciler Tamirhane'yi ilk kez bağlarken kendilerini burada
/// kaydeder ve bir client_id alır. Kayıt herkese açık; zarar verememesi için: yönlendirme adresi izin listesinde olmalı,
/// izinler sabit (sadece kod + yenileme, sadece "mcp" kapsamı, PKCE zorunlu), IP başına sınır, hiç kullanılmayan kayıt
/// bir gün sonra silinir (<see cref="OAuthMaintenance"/>).
/// </summary>
[ApiController]
[Route(OAuthServer.RegisterPath)]
[IgnoreAntiforgeryToken] // tarayıcı formu değil; istemci sunucudan JSON gönderir, çerez kullanılmaz
[EnableRateLimiting(McpRateLimit.RegisterPolicy)]
public sealed class ClientRegistrationController(
    IOpenIddictApplicationManager applications, AccountOptions options, TimeProvider clock) : ControllerBase
{
    public const string RegisteredAtProperty = "registered_at";
    private const int MaxRedirectUris = 5;
    private const int MaxNameLength = 100;

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest? request, CancellationToken ct)
    {
        var redirects = request?.RedirectUris ?? [];
        if (redirects.Count is 0 or > MaxRedirectUris || !redirects.All(RedirectAllowlist.IsAllowed))
            return Error("invalid_redirect_uri", "redirect_uris must be allowed client callback addresses");

        string[] allowedGrants = [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken];
        var grants = request!.GrantTypes ?? [GrantTypes.AuthorizationCode, GrantTypes.RefreshToken];
        if (!grants.All(allowedGrants.Contains) || !grants.Contains(GrantTypes.AuthorizationCode))
            return Error("invalid_client_metadata", "only authorization_code and refresh_token are supported");
        if (request.ResponseTypes is { } responseTypes && !responseTypes.All(r => r == ResponseTypes.Code))
            return Error("invalid_client_metadata", "only the code response type is supported");

        var method = request.TokenEndpointAuthMethod ?? ClientAuthenticationMethods.ClientSecretBasic;
        var isPublic = method == "none";
        if (!isPublic && method is not (ClientAuthenticationMethods.ClientSecretBasic or ClientAuthenticationMethods.ClientSecretPost))
            return Error("invalid_client_metadata", "unsupported token_endpoint_auth_method");

        // Ad izin ekranında görünür: kontrol karakterleri atılır, kısaltılır (görüntülenirken ayrıca HTML kaçışlanır).
        var name = new string((request.ClientName ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
            name = "MCP client";
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        var clientId = Guid.NewGuid().ToString("N");
        var secret = isPublic ? null : Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret, // OpenIddict hash'leyerek saklar
            ClientType = isPublic ? ClientTypes.Public : ClientTypes.Confidential,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = name,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + OAuthServer.Scope,
                Permissions.Prefixes.Resource + OAuthServer.Resource(options.PublicOrigin),
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        if (grants.Contains(GrantTypes.RefreshToken))
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        foreach (var uri in redirects)
            descriptor.RedirectUris.Add(new Uri(uri));
        descriptor.Properties[RegisteredAtProperty] = JsonSerializer.SerializeToElement(now.ToUnixTimeSeconds());

        await applications.CreateAsync(descriptor, ct);

        return StatusCode(StatusCodes.Status201Created, new RegistrationResponse(
            clientId, now.ToUnixTimeSeconds(), secret, secret is null ? null : 0, redirects, grants, [ResponseTypes.Code],
            method, name, OAuthServer.Scope));
    }

    private BadRequestObjectResult Error(string code, string description) =>
        BadRequest(new Dictionary<string, string> { ["error"] = code, ["error_description"] = description });

    public sealed record RegistrationRequest(
        [property: JsonPropertyName("redirect_uris")] List<string>? RedirectUris,
        [property: JsonPropertyName("client_name")] string? ClientName,
        [property: JsonPropertyName("grant_types")] List<string>? GrantTypes,
        [property: JsonPropertyName("response_types")] List<string>? ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod);

    public sealed record RegistrationResponse(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_id_issued_at")] long IssuedAt,
        [property: JsonPropertyName("client_secret"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ClientSecret,
        [property: JsonPropertyName("client_secret_expires_at"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? SecretExpiresAt,
        [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
        [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
        [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
        [property: JsonPropertyName("client_name")] string ClientName,
        [property: JsonPropertyName("scope")] string Scope);

    internal static long? RegisteredAt(ImmutableDictionary<string, JsonElement> properties) =>
        properties.TryGetValue(RegisteredAtProperty, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64() : null;
}
