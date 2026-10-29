using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Demo;
using Tamirhane.Web.Localization;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// "Demoyu dene" (#36): ziyaretçiye örnek verili kendi atölyesi açılır, sahibi olarak panele girer.
/// Demo, ziyaretçinin arayüz dilinde açılır. Açık bir oturum varsa (başka atölye) yerini demo oturumu alır.
/// </summary>
[Route("demo")]
public sealed class DemoController(IStringLocalizer<SharedResource> t) : Controller
{
    [HttpPost("")]
    [AllowAnonymous]
    [EnableRateLimiting(DemoRateLimit.Policy)]
    public async Task<IActionResult> Start([FromServices] StartDemo start, CancellationToken ct)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "tr";
        var result = await start.HandleAsync(language, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = t["demo.full"].Value;
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }
        return RedirectToAction(nameof(PanelController.Index), "Panel");
    }

    /// <summary>Demo şeridindeki "Kendi atölyeni aç": demo oturumu kapanır, kayıt formu açılır (demo verisi taşınmaz).</summary>
    [HttpPost("leave")]
    [Authorize]
    public async Task<IActionResult> Leave([FromServices] IAccountService accounts)
    {
        await accounts.SignOutAsync();
        return RedirectToAction(nameof(AccountController.Register), "Account");
    }
}
