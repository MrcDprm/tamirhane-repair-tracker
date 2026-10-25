using System.Net;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>Hesap e-postalarının metinleri (TR/EN). Ortak şablon ve tasarım e-posta kuyruğuyla gelecek (#19).</summary>
internal static class AccountEmails
{
    public static EmailMessage Confirmation(string to, string locale, Uri link) => locale == "en"
        ? Build(to, "Confirm your Tamirhane account",
            "Click the link below to confirm your email address. The link is valid for 24 hours.",
            "Confirm my email", link,
            "If you did not sign up for Tamirhane, you can ignore this email.")
        : Build(to, "Tamirhane hesabını doğrula",
            "E-posta adresini doğrulamak için aşağıdaki linke tıkla. Link 24 saat geçerli.",
            "E-postamı doğrula", link,
            "Tamirhane'ye kaydolmadıysan bu e-postayı yok sayabilirsin.");

    public static EmailMessage AlreadyRegistered(string to, string locale, Uri loginLink, Uri resetLink) => locale == "en"
        ? Build(to, "You already have a Tamirhane account",
            $"Someone tried to sign up with this email address, but it already has an account. You can sign in: {loginLink}",
            "Reset my password", resetLink,
            "If this wasn't you, you can ignore this email; your account has not changed.")
        : Build(to, "Bu adresle zaten bir Tamirhane hesabın var",
            $"Bu e-posta adresiyle kayıt denendi ama adresin zaten bir hesabı var. Giriş yapabilirsin: {loginLink}",
            "Şifremi sıfırla", resetLink,
            "Bu sen değilsen e-postayı yok sayabilirsin; hesabında bir değişiklik olmadı.");

    public static EmailMessage PasswordReset(string to, string locale, Uri link) => locale == "en"
        ? Build(to, "Reset your Tamirhane password",
            "Click the link below to choose a new password. The link is valid for 1 hour and works only once.",
            "Choose a new password", link,
            "If you did not ask for this, you can ignore this email; your password has not changed.")
        : Build(to, "Tamirhane şifreni sıfırla",
            "Yeni bir şifre belirlemek için aşağıdaki linke tıkla. Link 1 saat geçerli ve sadece bir kez kullanılabilir.",
            "Yeni şifre belirle", link,
            "Bunu sen istemediysen e-postayı yok sayabilirsin; şifren değişmedi.");

    private static EmailMessage Build(string to, string subject, string intro, string action, Uri link, string footer)
    {
        var text = $"{intro}{Environment.NewLine}{Environment.NewLine}{action}: {link}{Environment.NewLine}{Environment.NewLine}{footer}";
        var html = $"""
            <p>{WebUtility.HtmlEncode(intro)}</p>
            <p><a href="{WebUtility.HtmlEncode(link.AbsoluteUri)}">{WebUtility.HtmlEncode(action)}</a></p>
            <p style="color:#666">{WebUtility.HtmlEncode(footer)}</p>
            """;
        return new EmailMessage(to, subject, text, html);
    }
}
