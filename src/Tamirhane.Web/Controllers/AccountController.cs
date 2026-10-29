using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;
using Tamirhane.Web.Localization;
using Tamirhane.Web.Models.Account;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Kayıt, e-posta doğrulama, giriş ve çıkış. Hiçbir cevap "bu e-posta kayıtlı mı" sorusunu yanıtlamaz:
/// kayıt ve yeni link isteği her durumda aynı sayfaya gider, giriş hatası tek bir mesajdır.
/// </summary>
[Route("account")]
public sealed class AccountController(IAccountService accounts, IStringLocalizer<SharedResource> t) : Controller
{
    private const string InvalidLogin = "account.error.invalid_login";

    // Kullanım senaryosunun hata kodu → formdaki alan ve metnin anahtarı (Resources/*.json).
    private static readonly Dictionary<string, (string Field, string Message)> Errors = new()
    {
        [ShopErrors.InvalidName.Code] = (nameof(RegisterInput.ShopName), "account.error.shop_name"),
        [ShopErrors.InvalidLocale.Code] = (nameof(RegisterInput.Locale), "validation.language.required"),
        [ShopErrors.InvalidDisplayName.Code] = (nameof(RegisterInput.OwnerName), "account.error.name"),
        [AccountErrors.InvalidEmail.Code] = (nameof(RegisterInput.Email), "validation.email.invalid"),
        [AccountErrors.PasswordTooShort.Code] = (nameof(RegisterInput.Password), "account.error.password_short"),
        [AccountErrors.PasswordRequiresDigit.Code] = (nameof(RegisterInput.Password), "account.error.password_digit"),
        [AccountErrors.PasswordRequiresLetter.Code] = (nameof(RegisterInput.Password), "account.error.password_letter"),
        [AccountErrors.PasswordRepetitive.Code] = (nameof(RegisterInput.Password), "account.error.password_repetitive"),
        [AccountErrors.PasswordTooCommon.Code] = (nameof(RegisterInput.Password),
            "account.error.password_common"),
        [AccountErrors.InvalidResetLink.Code] = ("",
            "account.error.reset_link"),
    };

    [HttpGet("register")]
    public IActionResult Register() => SignedIn ? RedirectToPanel() : View(new RegisterInput());

    [HttpPost("register")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> Register(RegisterInput input, [FromServices] RegisterShop registerShop, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = await registerShop.HandleAsync(
            new RegisterShopCommand(input.ShopName, input.OwnerName, input.Email, input.Password, input.Locale), ct);
        if (result.IsFailure)
        {
            AddError(result.Error!);
            return View(input);
        }

        // E-posta zaten kayıtlıysa da buraya gelinir; adresin sahibine ayrı bir e-posta gitmiştir.
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("check-email")]
    public IActionResult CheckEmail() => View();

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string? token, CancellationToken ct)
    {
        var confirmed = ModelState.IsValid && token is not null && await accounts.ConfirmEmailAsync(userId, token, ct);
        return View(confirmed);
    }

    [HttpGet("resend-confirmation")]
    public IActionResult ResendConfirmation() => View(new EmailInput());

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> ResendConfirmation(EmailInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        await accounts.ResendEmailConfirmationAsync(input.Email, ct);
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) =>
        SignedIn ? RedirectToPanel() : View(new LoginInput { ReturnUrl = returnUrl });

    [HttpPost("login")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        switch (await accounts.SignInAsync(input.Email, input.Password, input.RememberMe, ct))
        {
            case SignInOutcome.Succeeded:
                // Sadece sitenin kendi adresine dönülür; "?ReturnUrl=https://kotu.site" ile dışarı yönlendirme yapılamaz.
                return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl) : RedirectToPanel();
            case SignInOutcome.EmailNotConfirmed:
                ViewData["EmailNotConfirmed"] = true;
                return View(input);
            default:
                ModelState.AddModelError("", t[InvalidLogin]);
                return View(input);
        }
    }

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new EmailInput());

    [HttpPost("forgot-password")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> ForgotPassword(EmailInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        // Kayıtlı olsun olmasın aynı sayfa: form "bu e-posta kayıtlı mı" sorusuna cevap vermez.
        await accounts.SendPasswordResetAsync(input.Email, ct);
        return RedirectToAction(nameof(ForgotPasswordSent));
    }

    [HttpGet("forgot-password-sent")]
    public IActionResult ForgotPasswordSent() => View();

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(Guid userId, string? token) =>
        View(new ResetPasswordInput { UserId = userId, Token = token ?? "" });

    [HttpPost("reset-password")]
    [EnableRateLimiting(AccountRateLimit.Policy)]
    public async Task<IActionResult> ResetPassword(ResetPasswordInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = await accounts.ResetPasswordAsync(input.UserId, input.Token, input.Password, ct);
        if (result.IsFailure)
        {
            AddError(result.Error!);
            return View(input);
        }
        return RedirectToAction(nameof(ResetPasswordDone));
    }

    [HttpGet("reset-password-done")]
    public IActionResult ResetPasswordDone() => View();

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await accounts.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private void AddError(Error error)
    {
        var (field, message) = MessageFor(error);
        ModelState.AddModelError(field, t[message]);
    }

    /// <summary>Hesap hatasının formdaki alanı ve metninin anahtarı; davet kabul formu da şifre hataları için kullanır.</summary>
    internal static (string Field, string Message) MessageFor(Error error) =>
        Errors.TryGetValue(error.Code, out var known)
            ? known
            : ("", "account.error.generic");

    private bool SignedIn => User.Identity?.IsAuthenticated == true;

    private RedirectToActionResult RedirectToPanel() => RedirectToAction(nameof(PanelController.Index), "Panel");
}
