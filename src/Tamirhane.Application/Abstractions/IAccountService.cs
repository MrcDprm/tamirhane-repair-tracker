using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Abstractions;

/// <summary>Davet e-postasının içeriği: kime, linkteki token, hangi atölye, kim davet etti, hangi dilde.</summary>
public sealed record StaffInvitationEmail(string Email, string Token, string ShopName, string InvitedBy, string Locale, DateTimeOffset ExpiresAt);

/// <summary>
/// Giriş hesapları (e-posta, şifre). Infrastructure Identity ile uygular; Application Identity'yi tanımaz.
/// E-postalardaki linkler isteğin Host başlığından değil uygulamanın kendi adresinden (PUBLIC_ORIGIN) üretilir.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Doğrulanmamış yeni hesap açar ve hemen kaydeder; kimliğini döner.
    /// E-posta zaten kayıtlıysa <see cref="Accounts.AccountErrors.EmailTaken"/>: çağıran bunu kullanıcıya göstermez.
    /// </summary>
    Task<Result<Guid>> CreateAsync(string email, string password, CancellationToken ct);

    /// <summary>
    /// Davetle gelen kişinin hesabı. Davet linki o adrese gittiği için adresin sahibi kanıtlanmıştır: hesap doğrulanmış açılır.
    /// E-posta zaten kayıtlıysa <see cref="Accounts.AccountErrors.EmailTaken"/>.
    /// </summary>
    Task<Result<Guid>> CreateConfirmedAsync(string email, string password, CancellationToken ct);

    /// <summary>Hesabı siler; açık oturumları da düşer (güvenlik damgası her istekte kontrol edilir).</summary>
    Task DeleteAsync(Guid userId, CancellationToken ct);

    /// <summary>Davet linkini gönderir. Token sadece bu e-postada bulunur.</summary>
    Task SendInvitationAsync(StaffInvitationEmail invitation, CancellationToken ct);

    /// <summary>E-posta doğrulama linkini gönderir.</summary>
    Task SendEmailConfirmationAsync(Guid userId, string locale, CancellationToken ct);

    /// <summary>Kayıt bu adresle tekrar denendiğinde adresin sahibine: "hesabın zaten var, giriş yap ya da şifreni sıfırla".</summary>
    Task SendAlreadyRegisteredAsync(string email, string locale, CancellationToken ct);

    /// <summary>
    /// E-posta ve şifreyle oturum açar. Kayıtsız e-posta, yanlış şifre ve kilitli hesap aynı sonucu verir;
    /// "e-posta doğrulanmamış" sadece şifre doğruysa söylenir.
    /// </summary>
    Task<SignInOutcome> SignInAsync(string email, string password, bool remember, CancellationToken ct);

    Task SignOutAsync();

    /// <summary>
    /// Demo atölyenin sahibi olarak şifresiz oturum açar ("Demoyu dene"). Sadece demo adresli hesaplarda çalışır;
    /// başka bir hesapla çağrılırsa hata fırlatır (bir hata yüzünden gerçek bir hesaba şifresiz girilemesin).
    /// </summary>
    Task SignInDemoAsync(Guid userId, CancellationToken ct);

    /// <summary>E-posta linkindeki token'la hesabı doğrular; link bozuksa ya da süresi geçmişse false.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);

    /// <summary>Hesap varsa ve doğrulanmamışsa yeni link gönderir; yoksa hiçbir şey yapmaz, çağıran farkı göstermez.</summary>
    Task ResendEmailConfirmationAsync(string email, CancellationToken ct);

    /// <summary>Hesap varsa şifre sıfırlama linki gönderir; yoksa hiçbir şey yapmaz, çağıran farkı göstermez.</summary>
    Task SendPasswordResetAsync(string email, CancellationToken ct);

    /// <summary>
    /// Linkteki token'la yeni şifreyi koyar. Link e-postaya geldiği için adresin sahibi olduğu da kanıtlanır: hesap
    /// doğrulanmış sayılır, kilit kalkar. Güvenlik damgası değişir; açık oturumlar kapanır, link ikinci kez çalışmaz.
    /// Link geçersizse <see cref="AccountErrors.InvalidResetLink"/>, şifre kurala uymuyorsa şifre hatası.
    /// </summary>
    Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct);
}
