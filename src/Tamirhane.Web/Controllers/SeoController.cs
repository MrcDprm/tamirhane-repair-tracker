using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Arama motorları için (#42): hangi sayfaların taranacağı (robots.txt) ve dizine girecek sayfaların iki dildeki
/// adresleri (sitemap.xml, hreflang ile). Uygulama sayfaları ve müşterinin takip linkleri taranmaz.
/// </summary>
[AllowAnonymous]
public sealed class SeoController : Controller
{
    /// <summary>Dizine girecek sayfalar (Türkçe yol; İngilizcesi /en ile).</summary>
    private static readonly string[] Pages = ["/", "/privacy", "/terms"];

    // Uygulamanın kendisi, hesap sayfaları ve takip linkleri: oturum ister ya da kişiye özeldir.
    private static readonly string[] Private =
        ["/panel", "/work-orders", "/inventory", "/staff", "/settings", "/account/", "/invitation", "/demo", "/t/", "/language", "/error"];

    [HttpGet("/robots.txt")]
    public ContentResult Robots()
    {
        var text = new StringBuilder("User-agent: *\n");
        foreach (var path in Private)
            text.Append($"Disallow: {path}\nDisallow: {LanguagePrefix.English}{path}\n");
        text.Append($"\nSitemap: {Origin}/sitemap.xml\n");
        return Content(text.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    public ContentResult Sitemap()
    {
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        var urls = Pages.SelectMany(page =>
        {
            var tr = Origin + page;
            var en = Origin + LanguagePrefix.English + page;
            XElement[] alternates =
            [
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "tr"), new XAttribute("href", tr)),
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "en"), new XAttribute("href", en)),
                new(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "x-default"), new XAttribute("href", tr)),
            ];
            return new[] { tr, en }.Select(loc => new XElement(sm + "url", new XElement(sm + "loc", loc), alternates));
        });
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(sm + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml), urls));
        return Content(document.Declaration + "\n" + document.Root, "application/xml; charset=utf-8");
    }

    // Yayında Caddy https ve alan adını iletir (ForwardedHeaders, AllowedHosts); adres isteğin kendisinden.
    private string Origin => $"{Request.Scheme}://{Request.Host}";
}
