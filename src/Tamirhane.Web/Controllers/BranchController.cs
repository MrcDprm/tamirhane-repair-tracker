using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.Controllers;

/// <summary>Menüdeki şube seçici: personel çalıştığı şubeyi değiştirir (sahip de teknisyen de).</summary>
[Authorize]
[Route("branch")]
public sealed class BranchController : Controller
{
    [HttpPost("switch")]
    public async Task<IActionResult> Switch(Guid branchId, string? returnUrl, [FromServices] SwitchBranch switchBranch, CancellationToken ct)
    {
        // Başka atölyenin şubesi "bulunamadı" döner; sonuç ne olursa olsun kullanıcı bulunduğu sayfaya döner.
        await switchBranch.HandleAsync(branchId, ct);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(PanelController.Index), "Panel");
    }
}
