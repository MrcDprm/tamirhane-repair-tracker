using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Dashboard;

namespace Tamirhane.Web.Controllers;

/// <summary>Atölye paneli (#33): günün sayıları, en uzun bekleyen işler, kritik stok, sahibe aylık ciro.</summary>
[Authorize]
[Route("panel")]
public sealed class PanelController : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromServices] GetDashboard dashboard, CancellationToken ct) =>
        View(await dashboard.HandleAsync(ct));
}
