using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Web.Models;

namespace Tamirhane.Web.Controllers;

/// <summary>Herkese açık sayfalar: tanıtım, gizlilik/KVKK aydınlatma metni, kullanım koşulları ve hata sayfası.</summary>
[AllowAnonymous]
public class HomeController : Controller
{
    // Oturumu açık personel tanıtımı değil işini görmek ister: doğrudan panele gider.
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Panel") : View();

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [HttpGet("/terms")]
    public IActionResult Terms() => View();

    /// <summary>
    /// Yakalanmayan hata (yayında, UseExceptionHandler) ve gövdesiz hata kodları (UseStatusCodePagesWithReExecute).
    /// Asıl isteğin yöntemiyle yeniden çalışır (POST da gelebilir), bu yüzden HttpGet değil. Durum kodu korunur.
    /// </summary>
    [Route("/error/{code:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code) =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, StatusCode = code });
}
