using System.Threading.RateLimiting;

namespace Tamirhane.Web.RateLimiting;

/// <summary>
/// Takip sayfasına IP başına sınır. Kod 60 bit olduğu için tahminle bulunamaz; bu sınır yine de kod taramayı ve
/// sayfanın toplu çekilmesini yavaşlatır. Müşteri sayfayı arada bir yeniler, dakikada 30 istek ona fazlasıyla yeter.
/// </summary>
public static class TrackingRateLimit
{
    public const string Policy = "tracking";
    public const int PermitsPerMinute = 30;

    // Sınırlayıcının ortak ayarları (429 yanıtı, Retry-After) AddAccountRateLimit'te; burada sadece politika eklenir.
    public static IServiceCollection AddTrackingRateLimit(this IServiceCollection services, string? clientIpHeader) =>
        services.AddRateLimiter(options => options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
            AccountRateLimit.ClientKey(context, clientIpHeader),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            })));
}
