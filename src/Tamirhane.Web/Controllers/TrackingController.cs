using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Web.Models.Tracking;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Müşterinin takip sayfası: fişteki QR ve bildirimlerdeki link buraya gelir (/t/7K3M9QX2HFDA). Giriş yok;
/// kod geçersiz de olsa kayıt yok da olsa aynı 404 sayfası döner. Teklif bu sayfadan onaylanır ya da reddedilir
/// (form CSRF token'lı; sayfa ve form aynı IP sınırına tabi).
/// </summary>
[AllowAnonymous]
[Route("t")]
[EnableRateLimiting(TrackingRateLimit.Policy)]
public sealed class TrackingController : Controller
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Show(string code, [FromServices] GetTracking get, CancellationToken ct)
    {
        KeepPrivate();
        var view = await get.HandleAsync(code, ct);
        return view is null ? Missing() : View(view);
    }

    [HttpPost("{code}/decision")]
    public async Task<IActionResult> Decide(
        string code, DecisionInput input, [FromServices] DecideQuoteByCustomer decide, [FromServices] GetTracking get, CancellationToken ct)
    {
        KeepPrivate();
        var result = await decide.HandleAsync(code, input.Approved, input.DecidedBy ?? "", ct);
        if (result.Error == ApplicationErrors.WorkOrderNotFound)
            return Missing();

        if (result.Error == QuoteErrors.DeciderNameRequired)
        {
            // Ad eksik: sayfa yazılanla birlikte yeniden gösterilir, karar verilmedi.
            var view = await get.HandleAsync(code, ct);
            if (view is null)
                return Missing();
            ViewData["DecisionError"] = "Kararı kimin verdiğini yazmak için adını ve soyadını gir (en fazla 100 karakter).";
            ViewData["DecidedBy"] = input.DecidedBy;
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return View(nameof(Show), view);
        }

        TempData[result.IsSuccess ? "Decision" : "DecisionError"] = result.IsSuccess
            ? input.Approved
                ? "Onayın atölyeye iletildi. Onarım başlayınca bu sayfada görünür."
                : "Kararın atölyeye iletildi. Cihazını teslim almak için atölyeyi arayabilirsin."
            // Karar zaten verilmiş (başka sekmeden ya da atölye telefonla kaydetmiş) ya da teklif artık beklemiyor.
            : "Bu teklif için karar zaten verilmiş; sayfadaki son durum geçerli.";
        return RedirectToAction(nameof(Show), new { code });
    }

    // Adresteki kod gizli bir anahtar gibi: başka siteye geçerken Referer ile gitmesin, arama motoru dizine eklemesin,
    // ortak bir bilgisayarda tarayıcı önbelleğinde kalmasın.
    private void KeepPrivate()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers.CacheControl = "no-store";
    }

    private ViewResult Missing()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }
}
