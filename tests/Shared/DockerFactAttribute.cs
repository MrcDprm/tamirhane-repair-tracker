using DotNet.Testcontainers.Configurations;

namespace Tamirhane.Tests;

/// <summary>
/// Docker gerektiren test. Docker çalışmıyorsa test kırmızı yanmaz, nedeni yazılarak atlanır;
/// böylece Docker'ı olmayan biri de diğer testleri çalıştırabilir.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public const string SkipReason =
        "Docker çalışmıyor; PostgreSQL entegrasyon testleri atlandı. Çalıştırmak için Docker Desktop'ı açın.";

    public DockerFactAttribute()
    {
        if (!DockerEngine.IsAvailable)
            Skip = SkipReason;
    }
}

public static class DockerEngine
{
    private static readonly Lazy<bool> Available = new(Ping);

    /// <summary>Docker'a bir kez sorulur; cevap test çalıştırması boyunca saklanır.</summary>
    public static bool IsAvailable => Available.Value;

    private static bool Ping()
    {
        try
        {
            var endpoint = TestcontainersSettings.OS.DockerEndpointAuthConfig;
            if (endpoint is null)
                return false;

            using var client = endpoint.GetDockerClientBuilder(Guid.NewGuid()).Build();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            client.System.PingAsync(timeout.Token).GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
