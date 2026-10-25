using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Tamirhane.Web.Controllers;

/// <summary>Atölye paneli. Oturumu olmayan giriş sayfasına yönlendirilir; asıl içerik #33'te.</summary>
[Authorize]
[Route("panel")]
public sealed class PanelController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
