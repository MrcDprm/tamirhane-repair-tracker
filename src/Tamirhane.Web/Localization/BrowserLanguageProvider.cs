using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;

namespace Tamirhane.Web.Localization;

/// <summary>
/// Çerez yoksa tarayıcının dili (Accept-Language): tercih sırasıyla ilk Türkçe ya da İngilizce. Hazır sağlayıcı
/// "en-US"yi desteklenen "en-GB" ile eşleştirmez; burada dilin kendisine bakılır. İkisi de yoksa varsayılan Türkçe.
/// </summary>
public sealed class BrowserLanguageProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.AcceptLanguage.ToString();
        if (!StringWithQualityHeaderValue.TryParseList(header.Split(','), out var languages))
            return NullProviderCultureResult;

        var culture = languages
            .Where(l => l.Quality is not 0)
            .OrderByDescending(l => l.Quality ?? 1)
            .Select(l => AppCultures.From(l.Value.Value))
            .FirstOrDefault(c => c is not null);
        return Task.FromResult(culture is null ? null : new ProviderCultureResult(culture.Name));
    }
}
