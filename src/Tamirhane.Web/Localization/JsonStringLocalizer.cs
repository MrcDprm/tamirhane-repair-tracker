using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Tamirhane.Web.Localization;

/// <summary>Bütün arayüz metinleri için tek kaynak (görünümler, denetleyiciler, doğrulama mesajları).</summary>
public sealed class SharedResource;

/// <summary>
/// Resources/tr.json ve en.json: düz { "anahtar": "metin" } sözlükleri, derlemeye gömülü (dosya kopyalamaya bağlı değil).
/// Uygulama açılırken bir kez okunur.
/// </summary>
public sealed class TextCatalog
{
    public static readonly TextCatalog Embedded = Load();

    private TextCatalog(FrozenDictionary<string, FrozenDictionary<string, string>> languages) => Languages = languages;

    /// <summary>Dil kodu ("tr", "en") → anahtar → metin.</summary>
    public FrozenDictionary<string, FrozenDictionary<string, string>> Languages { get; }

    public string? Find(string language, string key) =>
        Languages.TryGetValue(language, out var texts) && texts.TryGetValue(key, out var text) ? text : null;

    public static TextCatalog FromJson(IReadOnlyDictionary<string, string> jsonByLanguage) =>
        new(jsonByLanguage.ToFrozenDictionary(
            p => p.Key,
            p => (JsonSerializer.Deserialize<Dictionary<string, string>>(p.Value) ?? []).ToFrozenDictionary(StringComparer.Ordinal)));

    private static TextCatalog Load()
    {
        var assembly = typeof(TextCatalog).Assembly;
        var json = new Dictionary<string, string>();
        foreach (var language in new[] { "tr", "en" })
        {
            using var stream = assembly.GetManifestResourceStream($"Tamirhane.Web.Resources.{language}.json")
                ?? throw new InvalidOperationException($"Resources/{language}.json derlemeye gömülü değil.");
            using var reader = new StreamReader(stream);
            json[language] = reader.ReadToEnd();
        }
        return FromJson(json);
    }
}

/// <summary>
/// Şu anki arayüz diline göre metin. Anahtar o dilde yoksa Türkçesi, o da yoksa anahtarın kendisi döner
/// (ResourceNotFound işaretli): geçiş sırasında henüz anahtara çevrilmemiş Türkçe metinler olduğu gibi görünür.
/// </summary>
public sealed class JsonStringLocalizer(TextCatalog catalog) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var (text, found) = Lookup(name);
            return new LocalizedString(name, text, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var (text, found) = Lookup(name);
            return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, text, arguments), resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        catalog.Languages.TryGetValue(Language, out var texts)
            ? texts.Select(p => new LocalizedString(p.Key, p.Value))
            : [];

    private static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "tr";

    private (string Text, bool Found) Lookup(string name)
    {
        if (catalog.Find(Language, name) is { } text)
            return (text, true);
        if (catalog.Find("tr", name) is { } turkish)
            return (turkish, true);
        return (name, false);
    }
}

/// <summary>Her tür ve konum için aynı paylaşılan yerelleştirici (tek kaynak dosyası çifti).</summary>
public sealed class JsonStringLocalizerFactory(TextCatalog catalog) : IStringLocalizerFactory
{
    private readonly JsonStringLocalizer _localizer = new(catalog);

    public IStringLocalizer Create(Type resourceSource) => _localizer;

    public IStringLocalizer Create(string baseName, string location) => _localizer;
}
