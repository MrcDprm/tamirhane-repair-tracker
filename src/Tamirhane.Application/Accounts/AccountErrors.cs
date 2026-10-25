using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Accounts;

public static class AccountErrors
{
    public static readonly Error InvalidEmail = Error.Validation("account.invalid_email");
    public static readonly Error PasswordTooShort = Error.Validation("account.password_too_short");
    public static readonly Error PasswordRequiresDigit = Error.Validation("account.password_requires_digit");
    public static readonly Error PasswordRequiresLetter = Error.Validation("account.password_requires_letter");
    public static readonly Error PasswordRepetitive = Error.Validation("account.password_repetitive");
    public static readonly Error PasswordTooCommon = Error.Validation("account.password_too_common");

    /// <summary>Sadece içeride kullanılır: kullanıcıya gösterilirse "bu e-posta kayıtlı mı" bilgisi sızar.</summary>
    public static readonly Error EmailTaken = Error.Conflict("account.email_taken");
}
