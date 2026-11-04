using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Mcp;

/// <summary>Bir bağlı uygulama: istemcinin adı ve izin tarihi.</summary>
public sealed record ConnectionRow(string Id, string AppName, DateTimeOffset? CreatedAt);

public sealed record ConnectionsPage(string McpUrl, IReadOnlyList<ConnectionRow> Connections);

/// <summary>
/// "Yapay zekâ bağlantıları": MCP adresi, nasıl bağlanılacağı ve kişinin kendi izinleri. Her personel kendi bağlantılarını
/// görür ve keser (izin hesaba bağlı); kesince o izinle verilmiş bütün token'lar hemen geçersiz olur.
/// </summary>
[Authorize]
[Route("connections")]
public sealed class ConnectionsController(
    IOpenIddictAuthorizationManager authorizations, IOpenIddictApplicationManager applications, IOpenIddictTokenManager tokens,
    UserManager<ApplicationUser> users, AccountOptions options, IStringLocalizer<SharedResource> t) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var subject = users.GetUserId(User)!;
        var rows = new List<ConnectionRow>();
        await foreach (var authorization in authorizations.FindBySubjectAsync(subject, ct))
        {
            if (await authorizations.GetStatusAsync(authorization, ct) != OpenIddictConstants.Statuses.Valid)
                continue;
            var appId = await authorizations.GetApplicationIdAsync(authorization, ct);
            var app = appId is null ? null : await applications.FindByIdAsync(appId, ct);
            var name = app is null ? "?" : await applications.GetLocalizedDisplayNameAsync(app, ct) ?? "?";
            rows.Add(new ConnectionRow((await authorizations.GetIdAsync(authorization, ct))!, name,
                await authorizations.GetCreationDateAsync(authorization, ct)));
        }
        return View("~/Views/Mcp/Connections.cshtml",
            new ConnectionsPage(OAuthServer.Resource(options.PublicOrigin), rows.OrderByDescending(r => r.CreatedAt).ToList()));
    }

    [HttpPost("{id}/revoke")]
    public async Task<IActionResult> Revoke(string id, CancellationToken ct)
    {
        var authorization = await authorizations.FindByIdAsync(id, ct);
        // Başkasının izni "yok" sayılır: kimliği tahmin edilse bile kesilemez.
        if (authorization is null || await authorizations.GetSubjectAsync(authorization, ct) != users.GetUserId(User))
            return NotFound();

        await tokens.RevokeByAuthorizationIdAsync(id, ct);
        await authorizations.TryRevokeAsync(authorization, ct);
        TempData["Status"] = t["mcp.connections.revoked"].Value;
        return RedirectToAction(nameof(Index));
    }
}
