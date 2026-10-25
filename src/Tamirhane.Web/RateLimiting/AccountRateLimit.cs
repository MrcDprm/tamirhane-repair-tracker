using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace Tamirhane.Web.RateLimiting;

/// <summary>
/// Hesap formlarının (giriş, kayıt, şifre sıfırlama, yeni link) gönderimine IP başına sınır. Hesap kilidi tek bir
/// hesabı korur; bu sınır bir IP'nin çok sayıda hesabı denemesini ve e-posta bombardımanını yavaşlatır.
/// </summary>
public static class AccountRateLimit
{
    public const string Policy = "accounts";
    public const int PermitsPerMinute = 10;

    /// <param name="clientIpHeader">
    /// İstemcinin gerçek IP'sini taşıyan başlık (yayında Cloudflare'in CF-Connecting-IP'si). Sadece ayarla açılır:
    /// uygulamaya Cloudflare'i atlayarak gelen biri başlığı uydurup her istekte başka biri gibi görünebilirdi.
    /// </param>
    public static IServiceCollection AddAccountRateLimit(this IServiceCollection services, string? clientIpHeader)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context, clientIpHeader),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rejected, ct) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                response.ContentType = "text/plain; charset=utf-8";
                await response.WriteAsync("Çok fazla deneme yapıldı. Bir dakika sonra tekrar deneyin.", ct);
            };
        });
        return services;
    }

    /// <summary>
    /// Sınırın sayıldığı anahtar: istemcinin IP'si. IPv6'da adresin ilk yarısı (/64) kullanılır; bir ev ya da sunucu
    /// genelde bütün bir /64 bloğunu alır, tam adresle sayılsaydı her istekte adres değiştirilerek sınır aşılırdı.
    /// </summary>
    public static string ClientKey(HttpContext context, string? clientIpHeader)
    {
        var ip = clientIpHeader is not null
            && IPAddress.TryParse(context.Request.Headers[clientIpHeader].ToString(), out var forwarded)
                ? forwarded
                : context.Connection.RemoteIpAddress;
        if (ip is null)
            return "unknown";
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
            return ip.ToString();

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
