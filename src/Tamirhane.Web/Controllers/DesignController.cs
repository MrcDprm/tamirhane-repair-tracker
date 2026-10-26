using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Tasarım sisteminin vitrini: renkler, yazı tipleri ve bileşenler örnek verilerle tek sayfada.
/// Sadece geliştirmede açılır; yayında 404.
/// </summary>
[AllowAnonymous]
[Route("design")]
public sealed class DesignController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => environment.IsDevelopment() ? View() : NotFound();
}
