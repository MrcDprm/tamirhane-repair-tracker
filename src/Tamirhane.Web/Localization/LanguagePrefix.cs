using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Tamirhane.Web.Localization;

/// <summary>
/// Dil adreste (SEO, #42): Türkçe sayfalar önsüz (/privacy), İngilizceler /en altında (/en/privacy). Arama motoru iki dili
/// ayrı adres olarak görür (hreflang), aynı adres kişiye göre değişmez.
/// "/en" öneki istek başında temel yola (PathBase) alınır: uygulama yolları değişmeden çalışır, ASP.NET'in ürettiği bütün
/// linkler, yönlendirmeler ve giriş sayfasına dönüş kendiliğinden /en ile başlar.
/// </summary>
public static class LanguagePrefix
{
    public const string English = "/en";

    /// <summary>Bu dilin adres öneki: İngilizce "/en", Türkçe boş.</summary>
    public static string For(CultureInfo culture) => culture.TwoLetterISOLanguageName == "en" ? English : "";

    public static bool IsEnglish(HttpContext context) =>
        context.Request.PathBase.Value?.EndsWith(English, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// 1) "/en/..." isteğinde öneki temel yola taşır. 2) Önsüz bir sayfayı açan kişi daha önce İngilizceyi seçtiyse (dil
    /// çerezi) aynı sayfanın /en adresine yönlendirir. Arama motorunun çerezi olmadığı için önsüz adres onun için hep
    /// Türkçedir; tarayıcı dili (Accept-Language) bu yüzden yönlendirmede kullanılmaz.
    /// </summary>
    public static IApplicationBuilder UseLanguagePrefix(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Path.StartsWithSegments(English, StringComparison.OrdinalIgnoreCase, out var rest))
            {
                request.PathBase = request.PathBase.Add(English);
                request.Path = rest.HasValue ? rest : "/";
            }
            else if (PrefersEnglish(context) && IsPage(request))
            {
                context.Response.Redirect(request.PathBase + English + request.Path + request.QueryString);
                return;
            }
            await next();
        });

    /// <summary>Kültür adresten gelir: /en İngilizce, önsüz Türkçe.</summary>
    public sealed class Provider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) =>
            Task.FromResult<ProviderCultureResult?>(new((IsEnglish(httpContext) ? AppCultures.English : AppCultures.Turkish).Name));
    }

    private static bool PrefersEnglish(HttpContext context) =>
        context.Request.Cookies.TryGetValue(AppCultures.CookieName, out var cookie)
        && CookieRequestCultureProvider.ParseCookieValue(cookie)?.UICultures.FirstOrDefault() is { } chosen
        && AppCultures.From(chosen.Value)?.TwoLetterISOLanguageName == "en";

    // Yönlendirilen: tarayıcıda açılan sayfalar. Formlar (POST), dosyalar (site.css, qr.svg), sağlık kontrolü, dil seçici
    // ve müşterinin takip sayfası (dili atölyenin müşteri dilinden ya da ?lang'den gelir) olduğu gibi kalır.
    private static bool IsPage(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;
        var path = request.Path.Value ?? "/";
        if (Path.HasExtension(path))
            return false;
        string[] skipped = ["/t", "/health", "/healthz", "/language", "/error"];
        return !skipped.Any(s => request.Path.StartsWithSegments(s, StringComparison.OrdinalIgnoreCase));
    }
}
