using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Abstractions;

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

    /// <summary>E-posta linkindeki token'la hesabı doğrular; link bozuksa ya da süresi geçmişse false.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);

    /// <summary>Hesap varsa ve doğrulanmamışsa yeni link gönderir; yoksa hiçbir şey yapmaz, çağıran farkı göstermez.</summary>
    Task ResendEmailConfirmationAsync(string email, CancellationToken ct);
}
