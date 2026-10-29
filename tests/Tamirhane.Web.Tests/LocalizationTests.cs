using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Arayüz metinleri (#35): iki dosyada aynı anahtarlar ve yer tutucular, geri düşme sırası, ₺ simgesi, tarayıcı dili.
/// Docker gerekmez.
/// </summary>
public sealed partial class LocalizationTests
{
    private static readonly TextCatalog Catalog = TextCatalog.Embedded;

    [Fact]
    public void Both_files_have_the_same_keys()
    {
        var turkish = Catalog.Languages["tr"].Keys.ToHashSet();
        var english = Catalog.Languages["en"].Keys.ToHashSet();

        Assert.Empty(turkish.Except(english)); // İngilizcesi eksik
        Assert.Empty(english.Except(turkish)); // Türkçesi eksik
    }

    [Fact]
    public void Every_key_has_the_same_placeholders_in_both_languages()
    {
        var mismatched = Catalog.Languages["tr"]
            // Eksik anahtarı Both_files_have_the_same_keys bildirir; burada sadece iki dilde de olanlar karşılaştırılır.
            .Where(p => Catalog.Languages["en"].TryGetValue(p.Key, out var english)
                        && !Placeholders(p.Value).SetEquals(Placeholders(english)))
            .Select(p => p.Key)
            .ToList();

        Assert.Empty(mismatched);
    }

    [Fact]
    public void No_text_is_empty_and_keys_are_dotted_lowercase_names()
    {
        foreach (var (language, texts) in Catalog.Languages)
        foreach (var (key, text) in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{language}: {key} boş");
            Assert.Matches(KeyPattern(), key);
        }
    }

    [Fact]
    public void Text_comes_in_the_current_language_and_falls_back_to_turkish_then_to_the_key()
    {
        var catalog = TextCatalog.FromJson(new Dictionary<string, string>
        {
            ["tr"] = """{ "a": "Merhaba {0}", "only.tr": "Sadece Türkçe" }""",
            ["en"] = """{ "a": "Hello {0}" }""",
        });
        var localizer = new JsonStringLocalizer(catalog);

        using (UiCulture(AppCultures.English))
        {
            Assert.Equal("Hello Ayşe", localizer["a", "Ayşe"].Value);
            Assert.Equal("Sadece Türkçe", localizer["only.tr"].Value);
            var missing = localizer["Henüz anahtara çevrilmemiş metin"];
            Assert.Equal("Henüz anahtara çevrilmemiş metin", missing.Value);
            Assert.True(missing.ResourceNotFound);
        }
        using (UiCulture(AppCultures.Turkish))
            Assert.Equal("Merhaba Ayşe", localizer["a", "Ayşe"].Value);
    }

    [Fact]
    public void Prices_are_in_turkish_lira_in_both_languages()
    {
        Assert.Equal("₺1,200.50", 1200.50m.ToString("C", AppCultures.English));
        Assert.Equal("₺1.200,50", 1200.50m.ToString("C", AppCultures.Turkish));
        Assert.Equal("28/10/2026", new DateTime(2026, 10, 28).ToString("d", AppCultures.English));
    }

    [Theory]
    [InlineData("en-US,en;q=0.9", "en-GB")]
    [InlineData("de-DE,en;q=0.8,tr;q=0.5", "en-GB")] // Almanca desteklenmiyor, sıradaki İngilizce
    [InlineData("tr-TR,tr;q=0.9,en;q=0.8", "tr-TR")]
    [InlineData("en;q=0.3,tr;q=0.9", "tr-TR")]       // sıra değil kalite belirler
    [InlineData("de-DE,fr;q=0.8", null)]             // ikisi de yok: varsayılan Türkçe
    [InlineData("", null)]
    public async Task Browser_language_picks_turkish_or_english(string header, string? expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = header;

        var result = await new BrowserLanguageProvider().DetermineProviderCultureResult(context);

        Assert.Equal(expected, result?.Cultures.Single().Value);
    }

    [Theory]
    [InlineData("en", "en-GB")]
    [InlineData("EN-us", "en-GB")]
    [InlineData("tr", "tr-TR")]
    [InlineData("de", null)]
    [InlineData("entr", null)]
    [InlineData(null, null)]
    public void Language_codes_map_to_the_two_cultures(string? code, string? expected)
    {
        Assert.Equal(expected, AppCultures.From(code)?.Name);
    }

    private static HashSet<string> Placeholders(string text) => Placeholder().Matches(text).Select(m => m.Value).ToHashSet();

    private static IDisposable UiCulture(CultureInfo culture) => new CultureScope(culture);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture) => CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = _previous;
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^[a-z0-9]+(\.[a-z0-9_-]+)+$")]
    private static partial Regex KeyPattern();
}
