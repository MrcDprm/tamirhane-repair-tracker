using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Notifications;
using Tamirhane.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Tamirhane.Infrastructure.Tests;

/// <summary>
/// Testler için Docker'da geçici bir PostgreSQL açar ve migration'ları uygular.
/// Testler bitince konteyner silinir; gerçek veritabanına dokunulmaz.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public static readonly Uri PublicOrigin = new("https://tamirhane.test/");

    private PostgreSqlContainer? _container;
    private ServiceProvider _services = null!;

    public CapturingEmailSender Emails { get; } = new();

    /// <summary>Push servisi yerine: tarayıcı bildirimi istekleri buraya gelir.</summary>
    public FakePushService Push { get; } = new();

    /// <summary>Testler için üretilmiş VAPID anahtar çifti (her çalıştırmada yeni).</summary>
    public static readonly WebPushOptions PushOptions = NewVapidKeys();

    private static WebPushOptions NewVapidKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(true);
        return new WebPushOptions(Base64Url.EncodeToString([0x04, .. p.Q.X!, .. p.Q.Y!]), Base64Url.EncodeToString(p.D!),
            "mailto:test@tamirhane.test");
    }

    public async Task InitializeAsync()
    {
        // Docker yoksa testler [DockerFact] ile atlanır; xUnit fixture'ı yine de kurar, burada hiçbir şey yapılmaz.
        if (!DockerEngine.IsAvailable)
            return;

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(_container.GetConnectionString());
        services.AddAccounts(new AccountOptions(PublicOrigin, null, AccountOptions.DefaultFrom));
        services.AddSingleton<IEmailSender>(Emails); // gönderilen e-postalar testte okunur
        services.AddWebPush(PushOptions);
        services.AddHttpClient(DependencyInjection.WebPushClientName).ConfigurePrimaryHttpMessageHandler(() => Push);
        services.AddSingleton(new BackgroundJobOptions([TimeSpan.Zero, TimeSpan.Zero]));
        services.AddScoped<TestUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestUser>());
        _services = services.BuildServiceProvider();

        using var scope = AsShop(Guid.Empty);
        await scope.Db.Database.MigrateAsync();

        // Burada web sunucusu yok: arka plan işçisini (e-posta kuyruğu) elle başlat.
        foreach (var hosted in _services.GetServices<IHostedService>())
            await hosted.StartAsync(default);
        var queue = _services.GetRequiredService<BackgroundJobQueue>();
        Emails.BeforeRead = () => queue.WhenIdleAsync(TimeSpan.FromSeconds(10));
    }

    public async Task DisposeAsync()
    {
        if (_container is null)
            return;

        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Arka plan kuyruğundaki bütün işler (ve tekrar denemeleri) bitene kadar bekler.</summary>
    public Task IdleAsync() => _services.GetRequiredService<BackgroundJobQueue>().WhenIdleAsync(TimeSpan.FromSeconds(10));

    /// <summary>Uygulamadaki bir isteği taklit eder: verilen atölyenin personeli olarak yeni bir kapsam.</summary>
    public ShopScope AsShop(Guid shopId)
    {
        var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TestUser>().ShopId = shopId;
        return new ShopScope(scope);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

public sealed class TestUser : ICurrentUser
{
    public Guid ShopId { get; set; }
    public Guid StaffId { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; } = Guid.NewGuid();
    public bool IsOwner => true;
}

public sealed class ShopScope(IServiceScope scope) : IDisposable
{
    public ICurrentUser User => Get<ICurrentUser>();
    public TamirhaneDbContext Db => Get<TamirhaneDbContext>();
    public IWorkOrderRepository Orders => Get<IWorkOrderRepository>();
    public ICustomerRepository Customers => Get<ICustomerRepository>();
    public IPartRepository Parts => Get<IPartRepository>();
    public IStockRepository Stock => Get<IStockRepository>();
    public IUnitOfWork UnitOfWork => Get<IUnitOfWork>();
    public IAccountService Accounts => Get<IAccountService>();
    public UserManager<ApplicationUser> Users => Get<UserManager<ApplicationUser>>();

    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose() => scope.Dispose();
}
