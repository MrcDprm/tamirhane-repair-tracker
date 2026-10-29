using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Shops;
using Tamirhane.Web.Localization;
using Tamirhane.Web.Models.Staff;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// E-postadaki davet linki: teknisyen adını ve şifresini seçer, hesabı açılır ve oturumu başlar.
/// Geçersiz linkin nedeni (süresi dolmuş, kullanılmış, geri alınmış) ayrıca söylenmez.
/// </summary>
[Route("invitation")]
public sealed class InvitationController(IAccountService accounts, IStringLocalizer<SharedResource> t) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? token, [FromServices] GetInvitation get, CancellationToken ct)
    {
        var invitation = await get.HandleAsync(token, ct);
        return invitation.IsFailure
            ? View("Invalid")
            : View(new AcceptInvitationPage(invitation.Value, new AcceptInvitationInput { Token = token! }));
    }

    [HttpPost("")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> Index(
        [Bind(Prefix = nameof(AcceptInvitationPage.Input))] AcceptInvitationInput input,
        [FromServices] GetInvitation get, [FromServices] AcceptInvitation accept, CancellationToken ct)
    {
        var invitation = await get.HandleAsync(input.Token, ct);
        if (invitation.IsFailure)
            return View("Invalid");
        var page = new AcceptInvitationPage(invitation.Value, input);
        if (!ModelState.IsValid)
            return View(page);

        var result = await accept.HandleAsync(new AcceptInvitationCommand(input.Token, input.DisplayName, input.Password), ct);
        if (result.Error == ShopErrors.InvalidInvitation)
            return View("Invalid");
        if (result.IsFailure)
        {
            var (field, message) = result.Error == AccountErrors.EmailTaken
                ? ("", "invitation.error.email_taken")
                : result.Error == ShopErrors.InvalidDisplayName
                    ? (nameof(AcceptInvitationInput.DisplayName), "account.error.name")
                    : AccountController.MessageFor(result.Error!);
            ModelState.AddModelError(field == "" ? "" : $"{nameof(AcceptInvitationPage.Input)}.{field}", t[message]);
            return View(page);
        }

        // Hesap doğrulanmış açıldı; aynı şifreyle oturum hemen başlar.
        await accounts.SignInAsync(result.Value, input.Password, remember: false, ct);
        return RedirectToAction(nameof(PanelController.Index), "Panel");
    }
}
