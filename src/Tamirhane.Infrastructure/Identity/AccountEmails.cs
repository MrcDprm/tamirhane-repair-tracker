using System.Globalization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>Hesap e-postalarının metinleri (TR/EN); kalıp <see cref="EmailLayout"/>'ta.</summary>
internal static class AccountEmails
{
    public static EmailMessage Confirmation(string to, string locale, Uri link) => locale == "en"
        ? EmailLayout.Build(to, "en", "Confirm your Tamirhane account",
            ["Click the button below to confirm your email address. The link is valid for 24 hours."],
            new EmailAction("Confirm my email", link),
            "If you did not sign up for Tamirhane, you can ignore this email.")
        : EmailLayout.Build(to, "tr", "Tamirhane hesabını doğrula",
            ["E-posta adresini doğrulamak için aşağıdaki düğmeye tıkla. Link 24 saat geçerli."],
            new EmailAction("E-postamı doğrula", link),
            "Tamirhane'ye kaydolmadıysan bu e-postayı yok sayabilirsin.");

    public static EmailMessage AlreadyRegistered(string to, string locale, Uri loginLink, Uri resetLink) => locale == "en"
        ? EmailLayout.Build(to, "en", "You already have a Tamirhane account",
            ["Someone tried to sign up with this email address, but it already has an account.",
             $"You can sign in here: {loginLink.AbsoluteUri}"],
            new EmailAction("Reset my password", resetLink),
            "If this wasn't you, you can ignore this email; your account has not changed.")
        : EmailLayout.Build(to, "tr", "Bu adresle zaten bir Tamirhane hesabın var",
            ["Bu e-posta adresiyle kayıt denendi ama adresin zaten bir hesabı var.",
             $"Buradan giriş yapabilirsin: {loginLink.AbsoluteUri}"],
            new EmailAction("Şifremi sıfırla", resetLink),
            "Bu sen değilsen e-postayı yok sayabilirsin; hesabında bir değişiklik olmadı.");

    public static EmailMessage PasswordReset(string to, string locale, Uri link) => locale == "en"
        ? EmailLayout.Build(to, "en", "Reset your Tamirhane password",
            ["Click the button below to choose a new password. The link is valid for 1 hour and works only once."],
            new EmailAction("Choose a new password", link),
            "If you did not ask for this, you can ignore this email; your password has not changed.")
        : EmailLayout.Build(to, "tr", "Tamirhane şifreni sıfırla",
            ["Yeni bir şifre belirlemek için aşağıdaki düğmeye tıkla. Link 1 saat geçerli ve sadece bir kez kullanılabilir."],
            new EmailAction("Yeni şifre belirle", link),
            "Bunu sen istemediysen e-postayı yok sayabilirsin; şifren değişmedi.");

    public static EmailMessage Invitation(StaffInvitationEmail invitation, Uri link)
    {
        // Saat her zaman Türkiye saatiyle yazılır (veritabanından okunan değer UTC'dir).
        var english = invitation.Locale == "en";
        var expires = invitation.ExpiresAt.ToOffset(TimeSpan.FromHours(3))
            .ToString("d MMMM yyyy HH:mm", CultureInfo.GetCultureInfo(english ? "en-GB" : "tr-TR"));
        return english
            ? EmailLayout.Build(invitation.Email, "en", $"{invitation.ShopName} invited you to Tamirhane",
                [$"{invitation.InvitedBy} invited you to join {invitation.ShopName} on Tamirhane as a technician.",
                 $"Choose your name and password with the button below. The link works once and is valid until {expires} (Turkey time)."],
                new EmailAction("Accept the invitation", link),
                "If you don't know this shop, you can ignore this email; no account is created without the link.")
            : EmailLayout.Build(invitation.Email, "tr", $"{invitation.ShopName} seni Tamirhane'ye davet etti",
                [$"{invitation.InvitedBy}, seni Tamirhane'de {invitation.ShopName} atölyesine teknisyen olarak davet etti.",
                 $"Aşağıdaki düğmeyle adını ve şifreni belirle. Link bir kez kullanılabilir ve {expires} tarihine kadar geçerli."],
                new EmailAction("Daveti kabul et", link),
                "Bu atölyeyi tanımıyorsan e-postayı yok sayabilirsin; link kullanılmadan hesap açılmaz.");
    }
}
