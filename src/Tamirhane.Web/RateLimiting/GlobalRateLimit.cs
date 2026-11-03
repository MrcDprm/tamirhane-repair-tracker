using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Localization;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.RateLimiting;

/// <summary>
/// Bütün isteklere IP başına genel sınır ve ortak 429 cevabı. Formlara özel daha sıkı sınırlar (hesap, takip, demo)
/// bunun üstüne ayrıca uygulanır. Sınır bir sayfa açılışındaki statik dosyalarla bir atölyenin yoğun çalışmasını
/// rahatça karşılar; sadece otomatik, seri istekleri keser.
/// </summary>
public static class GlobalRateLimit
{
    public const int PermitsPerMinute = 600;

    public static IServiceCollection AddGlobalRateLimit(this IServiceCollection services, string? clientIpHeader)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                // Sağlık kontrolünü barındırma servisi sık sık çağırır; sınırlanmaz.
                context.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(AccountRateLimit.ClientKey(context, clientIpHeader),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = PermitsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Metin isteğin dilinde: dil seçimi (UseRequestLocalization) sınırlayıcıdan önce çalışır.
            options.OnRejected = async (rejected, ct) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                var t = rejected.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync(t["error.too_many_requests"], ct);
            };
        });
        return services;
    }
}
