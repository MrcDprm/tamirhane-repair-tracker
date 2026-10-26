using Microsoft.AspNetCore.Mvc;
using Tamirhane.Application.Shops;

namespace Tamirhane.Web.ViewComponents;

/// <summary>
/// Menüdeki şube seçici. Tek şubeli atölyede hiçbir şey çizmez: şube kavramı arayüzde görünmez.
/// </summary>
public sealed class BranchSwitcherViewComponent(ListBranches listBranches) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Content("");
        var branches = await listBranches.HandleAsync(HttpContext.RequestAborted);
        return branches.Count < 2 ? Content("") : View(branches);
    }
}
