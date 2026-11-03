namespace Tamirhane.Web.Security;

/// <summary>
/// Her cevaba (sayfa, statik dosya, hata) eklenen güvenlik başlıkları. HSTS ayrı: sadece yayında, UseHsts ile.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// Betik, stil, yazı tipi ve görseller sadece bu siteden; satır içi betik ve stil yok (görünümlerde yasak,
    /// DesignSystemTests denetler). Trusted Types: innerHTML gibi HTML yazan yerlere sadece "default" politikadan
    /// geçen metin girer (trusted-types.js), eval ve dışarıdan betik adresi tamamen kapalı. Site başka bir sitenin
    /// çerçevesine (iframe) konamaz, formlar sadece bu siteye gönderilir.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "worker-src 'self'; " +
        "manifest-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "require-trusted-types-for 'script'; " +
        "trusted-types default";

    /// <summary>Uygulamanın kullanmadığı tarayıcı özellikleri kapalı (bildirim izni Web Push için açık kalır).</summary>
    public const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY"; // frame-ancestors'ı tanımayan eski tarayıcılar için
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next();
        });
}
