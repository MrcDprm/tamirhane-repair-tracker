using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Tamirhane.Application.Abstractions;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// Cihaz şifresi Data Protection ile (AES, kimlik doğrulamalı) şifrelenir. Amaç dizesi ayrı: çerez ya da e-posta
/// token'ı için üretilmiş bir şifreli metin burada çözülemez, tersi de olmaz.
/// </summary>
internal sealed class PasscodeProtector(IDataProtectionProvider provider) : IPasscodeProtector
{
    private const string Prefix = "dp1:";
    private readonly IDataProtector _protector = provider.CreateProtector("Tamirhane.WorkOrder.Passcode.v1");

    public string Protect(string passcode) => Prefix + _protector.Protect(passcode);

    public string? Unprotect(string stored)
    {
        // Ön eki olmayan değer şifrelemeden önce kaydedildi (2026-11-03 öncesi); iş emri kapanınca zaten silinir.
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;
        try
        {
            return _protector.Unprotect(stored[Prefix.Length..]);
        }
        catch (CryptographicException)
        {
            return null; // anahtar kaybolduysa sayfa çökmesin; şifre müşteriden yeniden istenir
        }
    }
}
