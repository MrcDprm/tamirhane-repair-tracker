using System.Collections.Frozen;
using Microsoft.AspNetCore.Identity;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// Şifre kuralları: en az 10 karakter, en az bir harf ve bir rakam, sık kullanılan şifrelerden biri olmasın.
/// Büyük harf ve sembol zorunlu değil: uzunluk ve tahmin edilemezlik, karmaşıklık kuralından daha çok korur (NIST SP 800-63B).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    public static void Apply(PasswordOptions options)
    {
        options.RequiredLength = MinLength;
        options.RequireDigit = true;
        options.RequireLowercase = false;
        options.RequireUppercase = false;
        options.RequireNonAlphanumeric = false;
        options.RequiredUniqueChars = 4; // "aaaaaaaaa1" gibi şifreler geçmesin
    }
}

/// <summary>
/// Identity'nin kendi kurallarına eklenenler: harf şartı (Identity sadece büyük/küçük harf ayrı ayrı sorabiliyor)
/// ve sızıntı listelerinde en sık geçen şifreler (common-passwords.txt, kaynağı yanındaki LICENSE dosyasında).
/// </summary>
public sealed class PasswordPolicyValidator : IPasswordValidator<ApplicationUser>
{
    public const string RequiresLetterCode = "PasswordRequiresLetter";
    public const string TooCommonCode = "PasswordTooCommon";

    private const string ListResource = "Tamirhane.Infrastructure.Identity.common-passwords.txt";
    private static readonly Lazy<FrozenSet<string>> CommonPasswords = new(LoadList);

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        password ??= "";
        var errors = new List<IdentityError>();
        if (!password.Any(char.IsLetter))
            errors.Add(new IdentityError { Code = RequiresLetterCode, Description = "Şifrede en az bir harf olmalı." });
        if (CommonPasswords.Value.Contains(password.ToLowerInvariant()))
            errors.Add(new IdentityError { Code = TooCommonCode, Description = "Bu şifre çok yaygın." });

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }

    private static FrozenSet<string> LoadList()
    {
        using var stream = typeof(PasswordPolicyValidator).Assembly.GetManifestResourceStream(ListResource)
            ?? throw new InvalidOperationException($"Gömülü kaynak bulunamadı: {ListResource}");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
                passwords.Add(line);
        }
        return passwords.ToFrozenSet(StringComparer.Ordinal);
    }
}
