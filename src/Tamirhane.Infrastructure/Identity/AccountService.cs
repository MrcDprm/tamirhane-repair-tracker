using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure.Identity;

/// <summary><see cref="IAccountService"/>'in Identity ile uygulanması.</summary>
internal sealed class AccountService(
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TamirhaneDbContext db,
    IEmailSender email, AccountOptions options, ILogger<AccountService> logger)
    : IAccountService
{
    // Identity hata kodları → uygulamanın hata kodları. Şifre hataları kullanıcı hatası değil "kuralı anlat" mesajıdır.
    private static readonly Dictionary<string, Error> ErrorMap = new()
    {
        ["DuplicateEmail"] = AccountErrors.EmailTaken,
        ["DuplicateUserName"] = AccountErrors.EmailTaken,
        ["InvalidEmail"] = AccountErrors.InvalidEmail,
        ["InvalidUserName"] = AccountErrors.InvalidEmail,
        ["PasswordTooShort"] = AccountErrors.PasswordTooShort,
        ["PasswordRequiresDigit"] = AccountErrors.PasswordRequiresDigit,
        [PasswordPolicyValidator.RequiresLetterCode] = AccountErrors.PasswordRequiresLetter,
        ["PasswordRequiresUniqueChars"] = AccountErrors.PasswordRepetitive,
        [PasswordPolicyValidator.TooCommonCode] = AccountErrors.PasswordTooCommon,
    };

    public async Task<Result<Guid>> CreateAsync(string email, string password, CancellationToken ct)
    {
        email = (email ?? "").Trim();
        // Kullanıcı adı ayrı bir kavram değil: e-postanın kendisi.
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password ?? "");
        if (result.Succeeded)
            return user.Id;

        // Identity önce şifreyi doğrular: zayıf şifre + kayıtlı e-posta da şifre hatası döner, kayıt bilgisi sızmaz.
        return MapError(result);
    }

    public async Task SendEmailConfirmationAsync(Guid userId, string locale, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Doğrulama e-postası için hesap bulunamadı.");
        await SendConfirmationAsync(user, locale, ct);
    }

    public Task SendAlreadyRegisteredAsync(string email, string locale, CancellationToken ct)
    {
        var message = AccountEmails.AlreadyRegistered(email.Trim(), locale,
            Link("/account/login", []), Link("/account/forgot-password", []));
        return SendAsync(message, "zaten kayıtlı", ct);
    }

    public async Task<SignInOutcome> SignInAsync(string email, string password, bool remember, CancellationToken ct)
    {
        password ??= "";
        var user = await users.FindByEmailAsync((email ?? "").Trim());
        if (user is null)
        {
            // Kayıtsız e-postada da bir şifre özeti hesaplanır: cevabın süresinden adresin kayıtlı olduğu anlaşılmasın.
            users.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return SignInOutcome.InvalidCredentials;
        }

        // Identity'nin hazır PasswordSignInAsync'i kilidi ve doğrulanmamış hesabı şifreye bakmadan söyler
        // (kayıtlı olduğu sızar); sıra burada elle kurulur. Şifre kilitliyken de kontrol edilir ki süre aynı kalsın.
        var lockedOut = await users.IsLockedOutAsync(user);
        var passwordOk = await users.CheckPasswordAsync(user, password);
        if (lockedOut)
            return SignInOutcome.InvalidCredentials;
        if (!passwordOk)
        {
            await users.AccessFailedAsync(user); // 5. hatada 15 dakika kilit
            return SignInOutcome.InvalidCredentials;
        }
        if (!user.EmailConfirmed)
            return SignInOutcome.EmailNotConfirmed;

        await users.ResetAccessFailedCountAsync(user);
        await signIn.SignInAsync(user, isPersistent: remember);
        return SignInOutcome.Succeeded;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || Decode(token) is not { } decoded)
            return false;
        return (await users.ConfirmEmailAsync(user, decoded)).Succeeded;
    }

    public async Task ResendEmailConfirmationAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync((email ?? "").Trim());
        if (user is null || user.EmailConfirmed)
            return;

        await SendConfirmationAsync(user, await LocaleOfAsync(user.Id, ct), ct);
    }

    public async Task SendPasswordResetAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync((email ?? "").Trim());
        if (user is null)
            return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = Link("/account/reset-password", new() { ["userId"] = user.Id.ToString(), ["token"] = Encode(token) });
        await SendAsync(AccountEmails.PasswordReset(user.Email!, await LocaleOfAsync(user.Id, ct), link), "şifre sıfırlama", ct);
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || Decode(token) is not { } decoded)
            return AccountErrors.InvalidResetLink;

        // Identity önce token'a bakar (süresi, damgası), sonra şifre kurallarına; başarılıysa damgayı yeniler.
        var result = await users.ResetPasswordAsync(user, decoded, newPassword ?? "");
        if (!result.Succeeded)
            return result.Errors.First().Code == "InvalidToken" ? AccountErrors.InvalidResetLink : MapError(result);

        // Link e-postaya geldi: adresin sahibi kanıtlandı. Kilit de kalkar; yoksa yeni şifreyle 15 dk beklerdi.
        user.EmailConfirmed = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await users.UpdateAsync(user);
        return Result.Success();
    }

    // Hesabın atölyesinin dili. Oturum yokken çağrılır; kiracı filtresi bilerek kapatılır.
    private async Task<string> LocaleOfAsync(Guid userId, CancellationToken ct) =>
        await db.Staff.IgnoreQueryFilters()
            .Where(s => s.UserId == userId)
            .Join(db.Shops.IgnoreQueryFilters(), s => s.ShopId, shop => shop.Id, (_, shop) => shop.Locale)
            .FirstOrDefaultAsync(ct) ?? "tr";

    private static Error MapError(IdentityResult result)
    {
        var first = result.Errors.First();
        return ErrorMap.TryGetValue(first.Code, out var error)
            ? error
            : throw new InvalidOperationException($"Beklenmeyen Identity hatası: {first.Code}");
    }

    private async Task SendConfirmationAsync(ApplicationUser user, string locale, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = Link("/account/confirm-email", new() { ["userId"] = user.Id.ToString(), ["token"] = Encode(token) });
        await SendAsync(AccountEmails.Confirmation(user.Email!, locale, link), "doğrulama", ct);
    }

    /// <summary>
    /// Gönderim hatası işlemi bozmaz (hesap açılmış olur, link tekrar istenebilir); hata alıcı adresi olmadan loglanır.
    /// Kalıcı çözüm, tekrar deneyen e-posta kuyruğu (#19).
    /// </summary>
    private async Task SendAsync(EmailMessage message, string kind, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Hesap e-postası gönderilemedi ({Kind})", kind);
        }
    }

    private Uri Link(string path, Dictionary<string, string?> query) =>
        new(options.PublicOrigin, QueryHelpers.AddQueryString(path, query));

    // Token içinde '+' ve '/' olabilir; linkte bozulmasın diye Base64Url.
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? Decode(string? token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token ?? ""));
        }
        catch (FormatException)
        {
            return null; // linkle oynanmış
        }
    }
}
