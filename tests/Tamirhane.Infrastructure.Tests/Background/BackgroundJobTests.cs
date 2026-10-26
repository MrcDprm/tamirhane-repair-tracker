using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Background;

namespace Tamirhane.Infrastructure.Tests.Background;

/// <summary>Arka plan kuyruğu: istek beklemez, hata 3 kez denenir, loglarda kişisel veri yok. Docker gerekmez.</summary>
public sealed class BackgroundJobTests : IAsyncLifetime
{
    private readonly LogCapture _logs = new();
    private ServiceProvider _services = null!;

    private BackgroundJobQueue Queue => _services.GetRequiredService<BackgroundJobQueue>();

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(_logs));
        services.AddSingleton(new BackgroundJobOptions([TimeSpan.Zero, TimeSpan.Zero]));
        services.AddBackgroundJobs();
        services.AddSingleton<CallCounter>();
        services.AddScoped<IWorkOrderNotificationHandler, FlakyHandler>();
        services.AddScoped<IWorkOrderNotificationHandler, SteadyHandler>();
        _services = services.BuildServiceProvider();
        foreach (var hosted in _services.GetServices<IHostedService>())
            await hosted.StartAsync(default);
    }

    public async Task DisposeAsync()
    {
        foreach (var hosted in _services.GetServices<IHostedService>())
            await hosted.StopAsync(default);
        await _services.DisposeAsync();
    }

    [Fact]
    public async Task Enqueue_returns_without_waiting_for_the_job()
    {
        var release = new TaskCompletionSource();

        var started = DateTime.UtcNow;
        Queue.Enqueue(new BackgroundJob("yavaş iş", (_, _) => release.Task));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
        Assert.Equal(1, Queue.Pending);

        release.SetResult();
        await Queue.WhenIdleAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Failing_job_is_tried_three_times_and_the_next_job_still_runs()
    {
        var attempts = 0;
        var nextRan = false;

        Queue.Enqueue(new BackgroundJob("hep hata", (_, _) => { attempts++; throw new HttpRequestException("Resend 500"); }));
        Queue.Enqueue(new BackgroundJob("sonraki", (_, _) => { nextRan = true; return Task.CompletedTask; }));
        await Queue.WhenIdleAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, attempts);
        Assert.True(nextRan);
        Assert.Equal(2, _logs.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains("hep hata")));
        Assert.Single(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("hep hata"));
    }

    [Fact]
    public async Task Job_that_succeeds_on_retry_is_not_logged_as_an_error()
    {
        var attempts = 0;

        Queue.Enqueue(new BackgroundJob("ikinci denemede", (_, _) =>
            ++attempts == 1 ? throw new TimeoutException() : Task.CompletedTask));
        await Queue.WhenIdleAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, attempts);
        Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Error_text_that_contains_an_address_never_reaches_the_logs()
    {
        // Dış servisin hata metni alıcıyı içerebilir (ör. "ayse@example.com geçersiz"); logda sadece hatanın türü durur.
        Queue.Enqueue(new BackgroundJob("e-posta: doğrulama", (_, _) =>
            throw new InvalidOperationException("ayse@example.com adresine gönderilemedi")));
        await Queue.WhenIdleAsync(TimeSpan.FromSeconds(5));

        Assert.NotEmpty(_logs.Entries);
        Assert.DoesNotContain(_logs.Entries, e => e.Message.Contains("@") || e.Exception is not null);
        Assert.Contains(_logs.Entries, e => e.Message.Contains(nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task Each_notification_channel_runs_and_retries_on_its_own()
    {
        using var scope = _services.CreateScope();
        var counter = _services.GetRequiredService<CallCounter>();

        scope.ServiceProvider.GetRequiredService<INotificationQueue>()
            .Enqueue(new WorkOrderNotification(Guid.NewGuid(), Guid.NewGuid(), WorkOrderStatus.Ready));
        await Queue.WhenIdleAsync(TimeSpan.FromSeconds(5));

        // Hata veren kanal yeniden denendi; çalışan kanal müşteriye ikinci kez gitmedi.
        Assert.Equal(2, counter.Calls[nameof(FlakyHandler)]);
        Assert.Equal(1, counter.Calls[nameof(SteadyHandler)]);
    }

    private sealed class CallCounter
    {
        public ConcurrentDictionary<string, int> Calls { get; } = new();

        public int Count(string name) => Calls.AddOrUpdate(name, 1, (_, n) => n + 1);
    }

    private sealed class FlakyHandler(CallCounter counter) : IWorkOrderNotificationHandler
    {
        public Task HandleAsync(WorkOrderNotification notification, CancellationToken ct) =>
            counter.Count(nameof(FlakyHandler)) == 1 ? throw new HttpRequestException("geçici") : Task.CompletedTask;
    }

    private sealed class SteadyHandler(CallCounter counter) : IWorkOrderNotificationHandler
    {
        public Task HandleAsync(WorkOrderNotification notification, CancellationToken ct)
        {
            counter.Count(nameof(SteadyHandler));
            return Task.CompletedTask;
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    /// <summary>Loga yazılan her şeyi (biçimlenmiş mesaj ve istisna) saklar.</summary>
    private sealed class LogCapture : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries => _entries.ToList();

        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));

        public void Dispose() { }
    }
}
