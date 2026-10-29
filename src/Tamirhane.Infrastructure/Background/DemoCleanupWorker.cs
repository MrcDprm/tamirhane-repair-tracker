using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tamirhane.Application.Demo;

namespace Tamirhane.Infrastructure.Background;

/// <summary>
/// Süresi dolan demo atölyeleri saatte bir siler (#36). Uygulama açılınca da hemen bir kez çalışır: ücretsiz sunucu
/// boştayken uyuduğu için saatlik tur kaçmış olabilir. Hata verirse loglanır, bir sonraki turda yeniden denenir.
/// </summary>
internal sealed class DemoCleanupWorker(IServiceScopeFactory scopes, ILogger<DemoCleanupWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<DeleteExpiredDemos>().HandleAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Süresi dolan {Count} demo atölye silindi.", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError("Demo temizliği olmadı, bir sonraki turda yeniden denenecek ({Error}).", ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
