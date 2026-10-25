using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Identity;

/// <summary><see cref="IAccountService"/>'in Identity ile uygulanması.</summary>
internal sealed class AccountService(
    UserManager<ApplicationUser> users, IEmailSender email, AccountOptions options, ILogger<AccountService> logger)
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
        var first = result.Errors.First();
        return ErrorMap.TryGetValue(first.Code, out var error)
            ? error
            : throw new InvalidOperationException($"Beklenmeyen Identity hatası: {first.Code}");
    }

    public async Task SendEmailConfirmationAsync(Guid userId, string locale, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Doğrulama e-postası için hesap bulunamadı.");
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = Link("/account/confirm-email", new() { ["userId"] = user.Id.ToString(), ["token"] = Encode(token) });
        await SendAsync(AccountEmails.Confirmation(user.Email!, locale, link), "doğrulama", ct);
    }

    public Task SendAlreadyRegisteredAsync(string email, string locale, CancellationToken ct)
    {
        var message = AccountEmails.AlreadyRegistered(email.Trim(), locale,
            Link("/account/login", []), Link("/account/forgot-password", []));
        return SendAsync(message, "zaten kayıtlı", ct);
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
}
