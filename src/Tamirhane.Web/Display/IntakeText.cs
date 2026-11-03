using Microsoft.Extensions.Localization;

namespace Tamirhane.Web.Display;

/// <summary>
/// Kabul formundaki işaretlemeleri iş emrinin metin alanlarına çevirir ("Şarj aleti, Kılıf, kırmızı kalem"). Metinler
/// personelin o anki dilinde; fiş ve iş emri sayfası bunları olduğu gibi gösterir.
/// </summary>
public static class IntakeText
{
    /// <summary>
    /// Seçilen türün izin verdiği seçenekler (katalog sırasıyla) + serbest not. Bilinmeyen ya da başka türün anahtarı
    /// yok sayılır (formdan her değer gelebilir). Hiçbiri yoksa null.
    /// </summary>
    public static string? Join(IStringLocalizer t, string prefix, IEnumerable<string>? picked, IReadOnlyList<string> allowed, string? other)
    {
        var chosen = (picked ?? []).ToHashSet(StringComparer.Ordinal);
        var parts = allowed.Where(chosen.Contains).Select(key => t[prefix + key].Value).ToList();
        if (!string.IsNullOrWhiteSpace(other))
            parts.Add(other.Trim());
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>
    /// Ekran kilidi: türü kilit sormuyorsa ya da "yok" seçildiyse kaydedilmez; değer boşsa da kaydedilmez.
    /// Tür belirtilmemişse (eski form, testler) yazılan değer olduğu gibi kalır.
    /// </summary>
    public static string? Passcode(IStringLocalizer t, DeviceCatalog.Profile profile, string? lockType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (lockType is null)
            return value.Trim();
        if (!profile.AsksLock || lockType == "none" || !DeviceCatalog.LockTypes.Contains(lockType))
            return null;
        return $"{t["intake.lock." + lockType].Value}: {value.Trim()}";
    }
}
