namespace Tamirhane.Application.Abstractions;

/// <summary>
/// Cihaz şifresinin (ekran kilidi) veritabanında şifreli durması için. Infrastructure ASP.NET Data Protection ile uygular;
/// anahtarlar veritabanında değil sunucunun diskinde durur, veritabanı sızsa bile şifreler okunamaz.
/// </summary>
public interface IPasscodeProtector
{
    string Protect(string passcode);

    /// <summary>Şifreli değeri çözer. Şifrelemeden önce kaydedilmiş düz değer olduğu gibi döner; çözülemeyen değer null.</summary>
    string? Unprotect(string stored);
}

public static class PasscodeProtectorExtensions
{
    /// <summary>Boş şifre kaydedilmez (null); doluysa kırpılıp şifrelenir.</summary>
    public static string? ProtectOrNull(this IPasscodeProtector protector, string? passcode) =>
        string.IsNullOrWhiteSpace(passcode) ? null : protector.Protect(passcode.Trim());
}
