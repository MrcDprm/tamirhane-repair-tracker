using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Shops;

/// <summary>
/// Kiracı: sistemi kullanan atölye. Diğer bütün kayıtlar ShopId ile bir atölyeye bağlıdır.
/// Garanti süresi ve KDV oranı yeni iş emirleri ve teklif kalemleri için varsayılan değerdir.
/// </summary>
public sealed class Shop : Entity
{
    public const int MaxNameLength = 100;
    public const int DefaultWarrantyDays = 90;
    public const decimal DefaultVat = 20m;
    /// <summary>İş emrindeki garanti üst sınırıyla aynı (10 yıl).</summary>
    public const int MaxWarrantyDays = 3650;
    public static readonly IReadOnlyList<string> Locales = ["tr", "en"];

    private Shop() { }

    public string Name { get; private set; } = "";
    public int WarrantyDays { get; private set; }
    public decimal DefaultVatRate { get; private set; }
    /// <summary>Müşteriye giden e-postaların ve takip sayfasının varsayılan dili.</summary>
    public string Locale { get; private set; } = "tr";
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Shop> Create(string name, string locale, DateTimeOffset now)
    {
        var shop = new Shop { CreatedAt = now };
        var updated = shop.UpdateSettings(name, DefaultWarrantyDays, DefaultVat, locale);
        return updated.IsSuccess ? shop : updated.Error!;
    }

    /// <summary>
    /// Ayarlar sayfası. Garanti ve KDV sadece yeni iş emirlerine ve yeni teklif kalemlerine uygulanır;
    /// kayıtlı iş emirlerinin garantisi ve kalemlerin KDV'si değişmez.
    /// </summary>
    public Result UpdateSettings(string name, int warrantyDays, decimal defaultVatRate, string locale)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2 || name.Length > MaxNameLength)
            return ShopErrors.InvalidName;
        if (warrantyDays < 0 || warrantyDays > MaxWarrantyDays)
            return ShopErrors.InvalidWarrantyDays;
        // Para alanları gibi KDV de iki ondalıkla tutulur; "%20,125" yazılırsa sessizce yuvarlanmaz, reddedilir.
        if (defaultVatRate < 0 || defaultVatRate > 100 || decimal.Round(defaultVatRate, 2) != defaultVatRate)
            return ShopErrors.InvalidVatRate;
        if (!Locales.Contains(locale))
            return ShopErrors.InvalidLocale;

        Name = name;
        WarrantyDays = warrantyDays;
        DefaultVatRate = defaultVatRate;
        Locale = locale;
        return Result.Success();
    }
}
