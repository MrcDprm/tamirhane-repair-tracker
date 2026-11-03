using Tamirhane.Domain.Customers;

namespace Tamirhane.Web.Display;

/// <summary>
/// Cihaz kabul formunun türe göre değişen kısmı: hangi alanlar sorulur (IMEI, ekran kilidi), marka listesi, işaretlenecek
/// aksesuar ve görünür durum seçenekleri. Seçenekler metin anahtarıdır (intake.acc.*, intake.cond.*); kayıtta o anki
/// dildeki adlarıyla virgülle yazılır, fişte ve iş emrinde öyle görünür (veritabanı değişmez).
/// Model listesi bilerek kısa: yeni modeller her ay çıkar, liste sadece öneri verir; asıl öneriler atölyenin kendi geçmişi.
/// </summary>
public static class DeviceCatalog
{
    public sealed record Profile(
        DeviceKind Kind, bool AsksImei, bool AsksLock, IReadOnlyList<string> Brands,
        IReadOnlyList<string> Accessories, IReadOnlyList<string> Conditions);

    /// <summary>Ekran kilidi türleri; "none" seçilirse şifre alanı gizlenir ve kaydedilmez.</summary>
    public static readonly IReadOnlyList<string> LockTypes = ["none", "pin", "pattern", "password"];

    private static readonly string[] MobileConditions = ["screen_cracked", "back_glass", "scratched", "liquid", "no_power"];

    public static readonly IReadOnlyList<Profile> Profiles =
    [
        new(DeviceKind.Phone, true, true,
            ["Apple", "Samsung", "Xiaomi", "Huawei", "Oppo", "Realme", "Honor", "OnePlus", "Google", "Vivo", "Tecno",
             "Infinix", "Nokia", "Motorola", "General Mobile", "Reeder", "TCL", "Sony"],
            ["charger", "cable", "case", "sim", "memory_card", "box"], MobileConditions),
        new(DeviceKind.Tablet, true, true,
            ["Apple", "Samsung", "Xiaomi", "Huawei", "Lenovo", "Honor", "Reeder", "Microsoft", "TCL"],
            ["charger", "cable", "case", "pen", "sim", "box"], ["screen_cracked", "scratched", "liquid", "no_power"]),
        new(DeviceKind.Computer, false, true,
            ["Apple", "Lenovo", "HP", "Dell", "Asus", "Acer", "MSI", "Monster", "Casper", "Huawei", "Microsoft", "Samsung", "Toshiba"],
            ["adapter", "bag", "mouse", "box"], ["screen_cracked", "scratched", "broken_part", "missing_keys", "liquid", "no_power"]),
        new(DeviceKind.Desktop, false, true,
            ["Apple", "HP", "Dell", "Lenovo", "Asus", "MSI", "Casper", "Monster", "Acer"],
            ["power_cable", "keyboard", "mouse", "box"], ["scratched", "broken_part", "liquid", "no_power", "noise"]),
        new(DeviceKind.Watch, true, true,
            ["Apple", "Samsung", "Huawei", "Xiaomi", "Garmin", "Amazfit", "Fitbit"],
            ["charger", "strap", "box"], ["screen_cracked", "scratched", "liquid", "no_power"]),
        new(DeviceKind.Console, false, false,
            ["Sony", "Microsoft", "Nintendo", "Valve"],
            ["controller", "power_cable", "hdmi", "box"], ["scratched", "broken_part", "liquid", "no_power", "noise"]),
        new(DeviceKind.Tv, false, false,
            ["Samsung", "LG", "Sony", "Philips", "Vestel", "Arçelik", "Beko", "TCL", "Grundig", "Toshiba", "Hisense", "Xiaomi"],
            ["remote", "power_cable", "stand", "box", "warranty_doc"], ["screen_cracked", "scratched", "broken_part", "no_power"]),
        new(DeviceKind.Audio, false, false,
            ["Apple", "Samsung", "Sony", "JBL", "Bose", "Marshall", "Beats", "Sennheiser", "Harman Kardon", "Xiaomi", "Huawei", "Anker"],
            ["charger", "cable", "case", "box", "warranty_doc"], ["scratched", "broken_part", "liquid", "no_power", "noise"]),
        new(DeviceKind.Camera, false, false,
            ["Canon", "Nikon", "Sony", "Fujifilm", "Panasonic", "GoPro", "DJI", "Olympus"],
            ["charger", "battery", "lens", "memory_card", "bag", "box", "warranty_doc"],
            ["screen_cracked", "scratched", "broken_part", "liquid", "no_power"]),
        new(DeviceKind.Appliance, false, false,
            ["Philips", "Tefal", "Arzum", "Fakir", "Bosch", "Dyson", "Xiaomi", "Rowenta", "Braun", "Karaca", "Sinbo",
             "Arçelik", "Vestel", "De'Longhi", "Kiwi"],
            ["attachments", "power_cable", "box", "warranty_doc"], ["scratched", "broken_part", "burnt_smell", "no_power", "noise"]),
        new(DeviceKind.LargeAppliance, false, false,
            ["Arçelik", "Beko", "Vestel", "Bosch", "Siemens", "Samsung", "LG", "Profilo", "Altus", "Regal", "Indesit",
             "Electrolux", "Grundig", "Miele"],
            ["warranty_doc", "remote"], ["scratched", "broken_part", "water_leak", "burnt_smell", "no_power", "noise"]),
        new(DeviceKind.Other, false, false, [], ["charger", "cable", "box"], ["scratched", "broken_part", "no_power"]),
    ];

    /// <summary>Formda gösterilen sıra: telefondan beyaz eşyaya, en sonda "diğer".</summary>
    public static IReadOnlyList<DeviceKind> Kinds { get; } = Profiles.Select(p => p.Kind).ToList();

    /// <summary>Bütün türlerin aksesuar ve durum seçenekleri (form hepsini bir kez çizer, türe göre gizler).</summary>
    public static IReadOnlyList<string> AllAccessories { get; } = Profiles.SelectMany(p => p.Accessories).Distinct().ToList();

    public static IReadOnlyList<string> AllConditions { get; } = Profiles.SelectMany(p => p.Conditions).Distinct().ToList();

    public static Profile For(DeviceKind kind) => Profiles.FirstOrDefault(p => p.Kind == kind) ?? Profiles[^1];

    /// <summary>Bu seçeneği hangi türler gösterir ("Phone Tablet ..."): formdaki data-kinds özelliği.</summary>
    public static string KindsWithAccessory(string key) =>
        string.Join(' ', Profiles.Where(p => p.Accessories.Contains(key)).Select(p => p.Kind));

    public static string KindsWithCondition(string key) =>
        string.Join(' ', Profiles.Where(p => p.Conditions.Contains(key)).Select(p => p.Kind));

    public static string KindsWhere(Func<Profile, bool> predicate) =>
        string.Join(' ', Profiles.Where(predicate).Select(p => p.Kind));

    /// <summary>Hazır model önerileri: sadece en çok gelen marka ve türler. Gerisini atölyenin geçmişi tamamlar.</summary>
    public static IReadOnlyList<string> Models(DeviceKind kind, string? brand) =>
        brand is not null && ModelList.TryGetValue((kind, brand.Trim().ToLowerInvariant()), out var models) ? models : [];

    private static readonly Dictionary<(DeviceKind, string), string[]> ModelList = new()
    {
        [(DeviceKind.Phone, "apple")] =
        [
            "iPhone 16 Pro Max", "iPhone 16 Pro", "iPhone 16 Plus", "iPhone 16", "iPhone 16e",
            "iPhone 15 Pro Max", "iPhone 15 Pro", "iPhone 15 Plus", "iPhone 15",
            "iPhone 14 Pro Max", "iPhone 14 Pro", "iPhone 14 Plus", "iPhone 14",
            "iPhone 13 Pro Max", "iPhone 13 Pro", "iPhone 13", "iPhone 13 mini",
            "iPhone 12 Pro Max", "iPhone 12 Pro", "iPhone 12", "iPhone 12 mini",
            "iPhone 11 Pro Max", "iPhone 11 Pro", "iPhone 11", "iPhone SE (2022)", "iPhone SE (2020)",
            "iPhone XR", "iPhone XS Max", "iPhone XS", "iPhone X",
        ],
        [(DeviceKind.Phone, "samsung")] =
        [
            "Galaxy S25 Ultra", "Galaxy S25+", "Galaxy S25", "Galaxy S24 Ultra", "Galaxy S24+", "Galaxy S24", "Galaxy S24 FE",
            "Galaxy S23 Ultra", "Galaxy S23", "Galaxy S23 FE", "Galaxy S22 Ultra", "Galaxy S22", "Galaxy S21 FE",
            "Galaxy Z Fold6", "Galaxy Z Flip6", "Galaxy Z Fold5", "Galaxy Z Flip5",
            "Galaxy A56", "Galaxy A55", "Galaxy A54", "Galaxy A36", "Galaxy A35", "Galaxy A34", "Galaxy A26", "Galaxy A25",
            "Galaxy A16", "Galaxy A15", "Galaxy A14", "Galaxy A05s", "Galaxy M34",
        ],
        [(DeviceKind.Phone, "xiaomi")] =
        [
            "Redmi Note 14 Pro", "Redmi Note 14", "Redmi Note 13 Pro", "Redmi Note 13", "Redmi Note 12 Pro", "Redmi Note 12",
            "Redmi Note 11", "Redmi 14C", "Redmi 13C", "Redmi 13", "Redmi 12", "Xiaomi 15", "Xiaomi 14T Pro", "Xiaomi 14T",
            "Xiaomi 14", "Xiaomi 13T Pro", "Xiaomi 13T", "Poco X6 Pro", "Poco X6", "Poco F6", "Poco M6 Pro",
        ],
        [(DeviceKind.Phone, "huawei")] = ["P60 Pro", "P50 Pro", "P40 Lite", "P30 Pro", "P30", "P30 Lite", "Mate 50 Pro", "Nova 12", "Nova 11", "Nova 9"],
        [(DeviceKind.Tablet, "apple")] =
        [
            "iPad Pro 13 (M4)", "iPad Pro 11 (M4)", "iPad Air 13 (M2)", "iPad Air 11 (M2)", "iPad Air (5. nesil)",
            "iPad (10. nesil)", "iPad (9. nesil)", "iPad mini (7. nesil)", "iPad mini (6. nesil)",
        ],
        [(DeviceKind.Tablet, "samsung")] = ["Galaxy Tab S10 Ultra", "Galaxy Tab S10+", "Galaxy Tab S9 FE", "Galaxy Tab S9", "Galaxy Tab A9+", "Galaxy Tab A9", "Galaxy Tab A8"],
        [(DeviceKind.Computer, "apple")] =
        [
            "MacBook Air 13 (M3)", "MacBook Air 15 (M3)", "MacBook Air 13 (M2)", "MacBook Air 15 (M2)", "MacBook Air (M1)",
            "MacBook Pro 14 (M4)", "MacBook Pro 16 (M4)", "MacBook Pro 14 (M3)", "MacBook Pro 13 (M2)", "MacBook Pro 13 (M1)",
        ],
        [(DeviceKind.Computer, "lenovo")] = ["IdeaPad Slim 3", "IdeaPad Slim 5", "IdeaPad Gaming 3", "Legion 5", "Legion Pro 5", "ThinkPad E14", "ThinkPad T14", "ThinkBook 15", "Yoga Slim 7"],
        [(DeviceKind.Computer, "hp")] = ["Victus 15", "Victus 16", "Omen 16", "Pavilion 15", "Pavilion Aero 13", "ProBook 450", "EliteBook 840", "HP 250 G9", "HP 15s"],
        [(DeviceKind.Computer, "asus")] = ["VivoBook 15", "VivoBook 16", "ZenBook 14", "TUF Gaming F15", "TUF Gaming A15", "ROG Strix G16", "ROG Zephyrus G14"],
        [(DeviceKind.Computer, "dell")] = ["Inspiron 15", "Inspiron 14", "Vostro 15", "Latitude 5540", "XPS 13", "XPS 15", "G15"],
        [(DeviceKind.Computer, "monster")] = ["Abra A5", "Abra A7", "Tulpar T5", "Tulpar T7", "Huma H4", "Semruk S4"],
        [(DeviceKind.Watch, "apple")] = ["Apple Watch Series 10", "Apple Watch Series 9", "Apple Watch Series 8", "Apple Watch SE", "Apple Watch Ultra 2", "Apple Watch Ultra"],
        [(DeviceKind.Watch, "samsung")] = ["Galaxy Watch7", "Galaxy Watch6", "Galaxy Watch5", "Galaxy Watch Ultra", "Galaxy Watch4"],
        [(DeviceKind.Console, "sony")] = ["PlayStation 5", "PlayStation 5 Slim", "PlayStation 5 Pro", "PlayStation 4", "PlayStation 4 Slim", "PlayStation 4 Pro", "PlayStation Portal", "DualSense"],
        [(DeviceKind.Console, "microsoft")] = ["Xbox Series X", "Xbox Series S", "Xbox One X", "Xbox One S"],
        [(DeviceKind.Console, "nintendo")] = ["Switch 2", "Switch OLED", "Switch", "Switch Lite"],
        [(DeviceKind.Console, "valve")] = ["Steam Deck OLED", "Steam Deck"],
        [(DeviceKind.Audio, "apple")] = ["AirPods Pro 2", "AirPods Pro", "AirPods 4", "AirPods 3", "AirPods 2", "AirPods Max", "HomePod mini"],
        [(DeviceKind.Appliance, "dyson")] = ["V8", "V10", "V11", "V12 Detect Slim", "V15 Detect", "Gen5detect", "Airwrap", "Supersonic"],
    };
}
