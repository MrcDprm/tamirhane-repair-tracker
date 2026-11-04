using OpenIddict.Abstractions;

namespace Tamirhane.Web.Mcp;

/// <summary>
/// Altı saatte bir: süresi dolmuş ya da iptal edilmiş token ve izinler silinir; dinamik kayıtla eklenip bir gün içinde
/// hiç izin almamış istemciler silinir (kayıt herkese açık, tablo boşuna büyümesin).
/// </summary>
public sealed class OAuthMaintenance(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OAuthMaintenance> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    public static readonly TimeSpan UnusedClientAge = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "OAuth temizliği başarısız; bir sonraki turda tekrar denenecek.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var now = clock.GetUtcNow();

        // Pruning sadece belirtilen tarihten önce oluşturulmuş, geçersiz (süresi dolmuş/iptal) kayıtları siler.
        await tokens.PruneAsync(now - OAuthServer.RefreshTokenLifetime, ct);
        await authorizations.PruneAsync(now - OAuthServer.RefreshTokenLifetime, ct);

        var unused = new List<object>();
        await foreach (var application in applications.ListAsync(count: null, offset: null, ct))
        {
            var registeredAt = ClientRegistrationController.RegisteredAt(await applications.GetPropertiesAsync(application, ct));
            if (registeredAt is null || now - DateTimeOffset.FromUnixTimeSeconds(registeredAt.Value) < UnusedClientAge)
                continue;
            var id = await applications.GetIdAsync(application, ct);
            var hasAuthorization = false;
            await foreach (var _ in authorizations.FindByApplicationIdAsync(id!, ct))
            {
                hasAuthorization = true;
                break;
            }
            if (!hasAuthorization)
                unused.Add(application);
        }
        foreach (var application in unused)
            await applications.DeleteAsync(application, ct);
        if (unused.Count > 0)
            logger.LogInformation("Kullanılmayan {Count} OAuth istemcisi silindi.", unused.Count);
    }
}
