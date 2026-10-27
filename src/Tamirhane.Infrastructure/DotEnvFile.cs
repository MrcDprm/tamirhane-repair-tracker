namespace Tamirhane.Infrastructure;

/// <summary>
/// Geliştirmede repodaki .env dosyası (yayında değerler Render'ın ortam değişkenlerinden gelir). Dosya, verilen
/// klasörden yukarı doğru, çözüm dosyasının (Tamirhane.slnx) bulunduğu repo köküne kadar aranır; repo dışındaki
/// bir .env'e karışılmaz.
/// Satırlar düz karakter karşılaştırmasıyla ayrıştırılır: büyük-küçük harf dönüşümü ve kültüre bağlı düzenli ifade
/// yok. (Türkçe Windows'ta büyük/küçük harfe duyarsız eşleşme "VAPID"deki I'yı ı yapıp satırı kaçırıyordu.)
/// </summary>
public static class DotEnvFile
{
    public const string FileName = ".env";
    private const string RepoMarker = "Tamirhane.slnx";

    /// <summary>Başlangıç klasöründen repo köküne kadar ilk .env; yoksa null.</summary>
    public static string? Find(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(dir.FullName, RepoMarker)))
                return null; // repo kökü: daha yukarı çıkılmaz
        }
        return null;
    }

    /// <summary>Bulunan .env'in değerleri; dosya yoksa boş.</summary>
    public static IReadOnlyDictionary<string, string> Read(string startDirectory) =>
        Find(startDirectory) is { } path ? Parse(File.ReadAllText(path)) : new Dictionary<string, string>();

    /// <summary>
    /// ANAHTAR=değer satırları. Boş satır ve # ile başlayan yorum atlanır; "export " öneki kabul edilir; değer
    /// tek ya da çift tırnak içindeyse tırnaklar atılır. Değeri boş satır yok sayılır (.env.example'dan kopyalanan
    /// boş alanlar "ayarlı" sayılmasın). Geçersiz anahtarlı satır atlanır. Aynı anahtar iki kez varsa sonuncusu.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var equals = line.IndexOf('=');
            if (equals <= 0)
                continue;
            var key = line[..equals].Trim();
            var value = Unquote(line[(equals + 1)..].Trim());
            if (IsKey(key) && value.Length > 0)
                values[key] = value;
        }
        return values;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0] ? value[1..^1] : value;

    // Ortam değişkeni adı: harf ya da alt çizgiyle başlar, harf, rakam ve alt çizgiden oluşur (sadece ASCII).
    private static bool IsKey(string key) =>
        key.Length > 0 && (char.IsAsciiLetter(key[0]) || key[0] == '_')
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
