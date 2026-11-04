using System.Threading.RateLimiting;
using Tamirhane.Web.RateLimiting;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// MCP ve istemci kaydı için IP başına sınırlar (genel sınırın üstüne). Sınırlayıcı kimlik doğrulamadan önce çalışır,
/// bu yüzden hesap değil IP sayılır. Bir asistanın bir soruda birkaç araç çağırması sınırın çok altında kalır.
/// </summary>
public static class McpRateLimit
{
    public const string McpPolicy = "mcp";
    public const string RegisterPolicy = "oauth-register";
    public const int McpPermitsPerMinute = 120;
    public const int RegistrationsPerHour = 10;

    public static IServiceCollection AddMcpRateLimit(this IServiceCollection services, string? clientIpHeader)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(McpPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                AccountRateLimit.ClientKey(context, clientIpHeader),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = McpPermitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy(RegisterPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                AccountRateLimit.ClientKey(context, clientIpHeader),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = RegistrationsPerHour, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
        });
        return services;
    }
}
