using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Müşterinin takip sayfası: fişteki QR ve bildirimlerdeki link buraya gelir (/t/7K3M9QX2HFDA). Giriş yok;
/// kod geçersiz de olsa kayıt yok da olsa aynı 404 sayfası döner.
/// </summary>
[AllowAnonymous]
[Route("t")]
[EnableRateLimiting(TrackingRateLimit.Policy)]
public sealed class TrackingController : Controller
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Show(string code, [FromServices] GetTracking get, CancellationToken ct)
    {
        // Adresteki kod gizli bir anahtar gibi: başka siteye geçerken Referer ile gitmesin, arama motoru dizine eklemesin,
        // ortak bir bilgisayarda tarayıcı önbelleğinde kalmasın.
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-store";

        var view = await get.HandleAsync(code, ct);
        if (view is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("NotFound");
        }
        return View(view);
    }
}
