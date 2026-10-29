using System.Threading.RateLimiting;

namespace Tamirhane.Web.RateLimiting;

/// <summary>
/// Demo açmaya IP başına sınır: her demo veritabanında bir atölye dolusu kayıt demek. Bir ziyaretçiye saatte birkaç demo
/// fazlasıyla yeter; toplam üst sınır ayrıca StartDemo'da.
/// </summary>
public static class DemoRateLimit
{
    public const string Policy = "demo";
    public const int PermitsPerHour = 5;

    // Sınırlayıcının ortak ayarları (429 yanıtı, Retry-After) AddAccountRateLimit'te; burada sadece politika eklenir.
    public static IServiceCollection AddDemoRateLimit(this IServiceCollection services, string? clientIpHeader) =>
        services.AddRateLimiter(options => options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
            AccountRateLimit.ClientKey(context, clientIpHeader),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            })));
}
