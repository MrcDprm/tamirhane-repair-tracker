using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Tasarım sisteminin kuralları kaynak dosyalarda (docs/plan.md "Tasarım planı"): büyük harf sadece kaşede,
/// yazı tipleri ve betikler yerelden, sayfalarda satır içi betik/stil yok (CSP), dokunma hedefi 44 px. Docker gerekmez.
/// </summary>
public sealed partial class DesignSystemTests
{
    private static readonly string Web = Path.GetFullPath(Path.Combine(SourceDir(), "..", "..", "src", "Tamirhane.Web"));
    // Yorumlar atılır: kural adları ve seçiciler yorumla karışmasın.
    private static string Css => Regex.Replace(File.ReadAllText(Path.Combine(Web, "wwwroot", "css", "site.css")), @"/\*.*?\*/", "", RegexOptions.Singleline);
    private static IEnumerable<string> Views => Directory.EnumerateFiles(Path.Combine(Web, "Views"), "*.cshtml", SearchOption.AllDirectories);

    [Fact]
    public void Only_the_stamp_is_uppercase()
    {
        var rules = CssRule().Matches(Css).Where(m => m.Groups["body"].Value.Contains("uppercase")).Select(m => m.Groups["selector"].Value.Trim());
        Assert.Equal([".stamp"], rules);
    }

    [Fact]
    public void Fonts_and_scripts_are_served_locally()
    {
        Assert.DoesNotMatch(@"url\(\s*[""']?https?:", Css);
        foreach (var view in Views)
            Assert.DoesNotMatch(@"<(script|link)[^>]+(src|href)=""https?:", File.ReadAllText(view));
    }

    [Fact]
    public void Views_have_no_inline_scripts_styles_or_event_handlers()
    {
        foreach (var view in Views)
        {
            var html = File.ReadAllText(view);
            Assert.False(InlineScript().IsMatch(html), $"{Path.GetFileName(view)}: satır içi <script>");
            Assert.False(InlineStyle().IsMatch(html), $"{Path.GetFileName(view)}: style özelliği");
            Assert.False(EventHandler().IsMatch(html), $"{Path.GetFileName(view)}: onclick/onsubmit gibi satır içi olay");
        }
    }

    [Fact]
    public void Touch_targets_reduced_motion_and_focus_are_defined()
    {
        Assert.Contains("--touch: 44px;", Css);
        Assert.Contains("min-height: var(--touch)", Css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", Css);
        Assert.Contains(":focus-visible", Css);
    }

    [Fact]
    public void Light_and_dark_themes_define_the_same_tokens()
    {
        var light = Tokens(Regex.Match(Css, @":root \{(?<b>[^}]+)\}").Groups["b"].Value);
        var dark = Tokens(Regex.Match(Css, @":root\[data-theme=""dark""\] \{(?<b>[^}]+)\}").Groups["b"].Value);

        Assert.Empty(light.Where(t => t.StartsWith("--") && !t.StartsWith("--font") && !t.StartsWith("--radius")
            && t is not "--touch" and not "--gap" and not "--side").Except(dark));
    }

    private static IEnumerable<string> Tokens(string block) => Regex.Matches(block, @"(--[a-z-]+):").Select(m => m.Groups[1].Value);

    private static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex CssRule();

    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>")]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"\sstyle=""")]
    private static partial Regex InlineStyle();

    [GeneratedRegex(@"\son[a-z]+=""", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandler();
}
