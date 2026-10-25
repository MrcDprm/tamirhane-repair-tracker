namespace Tamirhane.Application.Accounts;

public enum SignInOutcome
{
    Succeeded,
    /// <summary>Kayıtsız e-posta, yanlış şifre ya da kilitli hesap: kullanıcıya hangisi olduğu söylenmez.</summary>
    InvalidCredentials,
    /// <summary>Şifre doğru ama e-posta doğrulanmamış. Şifreyi bilen birine söylemek bilgi sızdırmaz.</summary>
    EmailNotConfirmed,
}
