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
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2 || name.Length > MaxNameLength)
            return ShopErrors.InvalidName;
        if (!Locales.Contains(locale))
            return ShopErrors.InvalidLocale;

        return new Shop
        {
            Name = name,
            WarrantyDays = DefaultWarrantyDays,
            DefaultVatRate = DefaultVat,
            Locale = locale,
            CreatedAt = now,
        };
    }
}
