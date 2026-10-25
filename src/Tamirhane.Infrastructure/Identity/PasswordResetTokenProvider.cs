using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// Şifre sıfırlama token'ları. Identity'nin varsayılan sağlayıcısında bütün token'ların süresi tek (1 gün);
/// e-posta doğrulama linki 24 saat kalabilir ama hesabı ele geçirmeye yarayan sıfırlama linki 1 saat geçerli.
/// </summary>
public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtection,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtection, options, logger)
{
    public const string ProviderName = "PasswordReset";
}

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(1);
    }
}
