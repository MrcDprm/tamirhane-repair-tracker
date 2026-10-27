using System.Globalization;

namespace Tamirhane.Web.Localization;

/// <summary>
/// Arayüzün iki dili. İngilizce en-GB tabanlıdır (gün/ay sırası, 24 saat) ama para simgesi ₺: atölye Türkiye'de,
/// fiyatlar her iki dilde de Türk lirası. en-GB'nin kendi simgesi £ olurdu.
/// </summary>
public static class AppCultures
{
    public const string CookieName = "__Host-tamirhane-lang";

    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly CultureInfo English = CreateEnglish();

    public static readonly IReadOnlyList<CultureInfo> All = [Turkish, English];

    /// <summary>"tr", "en", "tr-TR", "en-US"... dilin kendisine bakılır; tanınmayan değer null.</summary>
    public static CultureInfo? From(string? value)
    {
        var code = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
            return null;
        if (code == "tr" || code.StartsWith("tr-", StringComparison.Ordinal))
            return Turkish;
        if (code == "en" || code.StartsWith("en-", StringComparison.Ordinal))
            return English;
        return null;
    }

    private static CultureInfo CreateEnglish()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        culture.NumberFormat.CurrencySymbol = "₺";
        return CultureInfo.ReadOnly(culture);
    }
}
