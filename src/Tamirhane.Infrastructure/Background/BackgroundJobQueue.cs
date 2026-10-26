using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Tamirhane.Infrastructure.Background;

/// <summary>
/// Arka planda yapılacak bir iş (e-posta gönderimi, bildirim). Kendi DI kapsamında çalışır: isteğin kapsamı
/// (DbContext, oturum) iş çalıştığında çoktan kapanmıştır. Name loglara yazılır; içinde kişisel veri olmamalı.
/// </summary>
public sealed record BackgroundJob(string Name, Func<IServiceProvider, CancellationToken, Task> Run);

/// <summary>
/// İşlerin sırası. İstek işi ekleyip hemen döner; <see cref="BackgroundJobWorker"/> sırayla çalıştırır.
/// Kuyruk bellekte: sunucu yeniden başlarsa bekleyen işler kaybolur (kabul edilen bedel; hesap linkleri yeniden istenebilir).
/// </summary>
public sealed class BackgroundJobQueue(ILogger<BackgroundJobQueue> logger)
{
    /// <summary>Taşma sınırı: bir hata döngüsü belleği doldurmasın.</summary>
    public const int Capacity = 1000;

    private readonly Channel<BackgroundJob> _channel = Channel.CreateBounded<BackgroundJob>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private int _pending;

    internal ChannelReader<BackgroundJob> Reader => _channel.Reader;

    /// <summary>Kuyrukta bekleyen ya da çalışmakta olan iş sayısı.</summary>
    public int Pending => Volatile.Read(ref _pending);

    public void Enqueue(BackgroundJob job)
    {
        Interlocked.Increment(ref _pending);
        if (_channel.Writer.TryWrite(job))
            return;

        Interlocked.Decrement(ref _pending);
        logger.LogError("Arka plan kuyruğu dolu, iş atlandı: {Job}", job.Name);
    }

    internal void Completed() => Interlocked.Decrement(ref _pending);

    /// <summary>Bütün işler bitene kadar bekler (testler e-postayı okumadan önce).</summary>
    public async Task WhenIdleAsync(TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (Pending > 0)
        {
            if (DateTime.UtcNow > until)
                throw new TimeoutException($"Arka plan kuyruğu {timeout} içinde boşalmadı ({Pending} iş).");
            await Task.Delay(10);
        }
    }
}
