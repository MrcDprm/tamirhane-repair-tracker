using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.Models.Staff;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>Personel sayfası: çalışanlar, bekleyen davetler, davet gönderme, daveti geri alma, personel çıkarma. Sadece sahip.</summary>
[Authorize(Policy = Policies.OwnerOnly)]
[Route("staff")]
public sealed class StaffController : Controller
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        [ShopErrors.InvalidEmail.Code] = "Geçerli bir e-posta adresi yaz.",
        [ShopErrors.CannotRemoveOwner.Code] = "Atölyenin sahibi kendini çıkaramaz.",
        [ShopErrors.StaffNotFound.Code] = "Bu kişi atölyede çalışmıyor.",
        [ApplicationErrors.InvitationNotFound.Code] = "Bu davet bulunamadı.",
        [ApplicationErrors.TooManyInvitations.Code] =
            $"En fazla {InviteStaff.MaxPendingInvitations} bekleyen davet olabilir. Kullanılmayan davetleri geri al.",
    };

    [HttpGet("")]
    public Task<IActionResult> Index([FromServices] ListStaff list, CancellationToken ct) =>
        PageAsync(list, new InviteInput(), ct);

    [HttpPost("invite")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> Invite(
        [Bind(Prefix = nameof(StaffPage.Invite))] InviteInput input,
        [FromServices] InviteStaff invite, [FromServices] ListStaff list, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await PageAsync(list, input, ct);

        var result = await invite.HandleAsync(input.Email, ct);
        if (result.IsFailure)
        {
            if (result.Error!.Kind == ErrorKind.Forbidden)
                return Forbid();
            ModelState.AddModelError($"{nameof(StaffPage.Invite)}.{nameof(InviteInput.Email)}", MessageFor(result.Error));
            return await PageAsync(list, input, ct);
        }

        TempData["Status"] = $"{input.Email.Trim()} adresine davet gönderildi. Link 7 gün geçerli ve bir kez kullanılabilir.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromServices] RevokeInvitation revoke, CancellationToken ct) =>
        Done(await revoke.HandleAsync(id, ct), "Davet geri alındı; link artık çalışmıyor.");

    [HttpPost("{id:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, [FromServices] RemoveStaff remove, CancellationToken ct) =>
        Done(await remove.HandleAsync(id, ct), "Personel çıkarıldı; hesabı silindi ve açık oturumu kapandı.");

    private async Task<IActionResult> PageAsync(ListStaff list, InviteInput invite, CancellationToken ct)
    {
        var overview = await list.HandleAsync(ct);
        return overview.IsFailure ? Forbid() : View(nameof(Index), new StaffPage(overview.Value, invite));
    }

    private IActionResult Done(Result result, string success)
    {
        if (result.Error?.Kind == ErrorKind.Forbidden)
            return Forbid();
        TempData[result.IsSuccess ? "Status" : "Error"] = result.IsSuccess ? success : MessageFor(result.Error!);
        return RedirectToAction(nameof(Index));
    }

    private static string MessageFor(Error error) =>
        Messages.GetValueOrDefault(error.Code, "İşlem tamamlanamadı. Sayfayı yenileyip tekrar dene.");
}
