using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tamirhane.Infrastructure.Background;

/// <summary>Denemeler arasındaki bekleme. 3 deneme = 2 bekleme; testlerde sıfır.</summary>
public sealed record BackgroundJobOptions(IReadOnlyList<TimeSpan> RetryDelays)
{
    public static readonly BackgroundJobOptions Default = new([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]);

    public int MaxAttempts => RetryDelays.Count + 1;
}

/// <summary>
/// Kuyruktaki işleri sırayla çalıştırır. Hata alan iş beklenip yeniden denenir; son denemede de olmazsa bırakılır.
/// Logda işin adı ve hatanın türü durur, mesajı durmaz: dış servislerin hata metni alıcı adresini içerebilir.
/// </summary>
internal sealed class BackgroundJobWorker(
    BackgroundJobQueue queue, IServiceScopeFactory scopes, BackgroundJobOptions options, ILogger<BackgroundJobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunWithRetriesAsync(job, stoppingToken);
            }
            finally
            {
                queue.Completed();
            }
        }
    }

    private async Task RunWithRetriesAsync(BackgroundJob job, CancellationToken stoppingToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await job.Run(scope.ServiceProvider, stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (attempt >= options.MaxAttempts)
                {
                    logger.LogError("Arka plan işi {Attempts} denemede de olmadı, bırakıldı: {Job} ({Error})",
                        attempt, job.Name, ex.GetType().Name);
                    return;
                }
                logger.LogWarning("Arka plan işi olmadı, yeniden denenecek ({Attempt}/{Max}): {Job} ({Error})",
                    attempt, options.MaxAttempts, job.Name, ex.GetType().Name);
                await Task.Delay(options.RetryDelays[attempt - 1], stoppingToken);
            }
        }
    }
}
