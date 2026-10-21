using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Tamirhane.Infrastructure.Tests;

/// <summary>
/// Testler için Docker'da geçici bir PostgreSQL açar ve migration'ları uygular.
/// Testler bitince konteyner silinir; gerçek veritabanına dokunulmaz.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        // Docker yoksa testler [DockerFact] ile atlanır; xUnit fixture'ı yine de kurar, burada hiçbir şey yapılmaz.
        if (!DockerEngine.IsAvailable)
            return;

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddInfrastructure(_container.GetConnectionString());
        services.AddScoped<TestUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestUser>());
        _services = services.BuildServiceProvider();

        using var scope = AsShop(Guid.Empty);
        await scope.Db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is null)
            return;

        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }

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
    public Guid StaffId { get; } = Guid.NewGuid();
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

    private T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public void Dispose() => scope.Dispose();
}
