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
    public async Task<IActionResult> Show(string code, [FromServices] GetTracking get, [FromServices] IPushSettings push, CancellationToken ct)
    {
        KeepPrivate();
        ViewData["PushKey"] = push.PublicKey; // boşsa "haber ver" düğmesi çıkmaz
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

    // --- Tarayıcı bildirimi (#31): tracking.js JSON gönderir, CSRF token'ı RequestVerificationToken başlığında. ---

    /// <summary>Bu tarayıcı bu iş emrine abone mi? Gövdede tarayıcının adresi (adresler URL'ye yazılmaz).</summary>
    [HttpPost("{code}/push/state")]
    [Consumes("application/json")]
    [RequestSizeLimit(PushBodyLimit)]
    public async Task<IActionResult> PushState(
        string code, [FromBody] PushInput input, [FromServices] IPushSettings push, [FromServices] GetPushState get, CancellationToken ct)
    {
        KeepPrivate();
        if (push.PublicKey is null)
            return NotFound();
        var subscribed = await get.HandleAsync(code, input.Endpoint, ct);
        return subscribed is null ? NotFound() : Ok(new { subscribed });
    }

    [HttpPost("{code}/push")]
    [Consumes("application/json")]
    [RequestSizeLimit(PushBodyLimit)]
    public async Task<IActionResult> Subscribe(
        string code, [FromBody] PushInput input, [FromServices] IPushSettings push, [FromServices] SubscribeToPush subscribe,
        CancellationToken ct)
    {
        KeepPrivate();
        if (push.PublicKey is null)
            return NotFound();
        var result = await subscribe.HandleAsync(code, new PushSubscriptionInput(input.Endpoint, input.Keys?.P256dh, input.Keys?.Auth), ct);
        if (result.Error == ApplicationErrors.WorkOrderNotFound)
            return NotFound();
        // Geçersiz abonelik ya da kapanmış iş emri: ayrıntı verilmez, düğme genel bir mesaj gösterir.
        return result.IsSuccess ? NoContent() : BadRequest();
    }

    [HttpPost("{code}/push/remove")]
    [Consumes("application/json")]
    [RequestSizeLimit(PushBodyLimit)]
    public async Task<IActionResult> Unsubscribe(
        string code, [FromBody] PushInput input, [FromServices] IPushSettings push, [FromServices] UnsubscribeFromPush unsubscribe,
        CancellationToken ct)
    {
        KeepPrivate();
        if (push.PublicKey is null)
            return NotFound();
        var result = await unsubscribe.HandleAsync(code, input.Endpoint, ct);
        return result.IsSuccess ? NoContent() : NotFound();
    }

    // Abonelik gövdesi birkaç yüz bayttır; büyük gövde hiç okunmaz.
    private const int PushBodyLimit = 4096;

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
