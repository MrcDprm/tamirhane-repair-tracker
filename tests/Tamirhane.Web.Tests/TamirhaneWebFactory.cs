using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Uygulamanın tamamını (Program.cs'teki bütün kayıtlarla) bellekte açar; veritabanı Docker'daki geçici PostgreSQL'dir.
/// Testler bitince konteyner silinir; gerçek veritabanına dokunulmaz.
/// </summary>
public sealed class TamirhaneWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public async Task InitializeAsync()
    {
        // Docker yoksa testler [DockerFact] ile atlanır; xUnit fixture'ı yine de kurar, burada hiçbir şey yapılmaz.
        if (!DockerEngine.IsAvailable)
            return;

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TamirhaneDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Program.cs bağlantıyı önce DATABASE_URL'den okur; böylece appsettings.Development.json'daki yerel veritabanı kullanılmaz.
        builder.UseSetting("DATABASE_URL", _container!.GetConnectionString());

    /// <summary>
    /// İşi, uygulamanın ayarlarıyla ve verilen atölyenin personeli gibi çalışan bir DbContext ile yapar.
    /// Giriş sistemi (#16) gelene kadar test verisini bununla hazırlarız.
    /// </summary>
    public async Task AsShopAsync(Guid shopId, Func<TamirhaneDbContext, Task> work)
    {
        // Seçenekler kapsama bağlıdır (EF kayıt servislerini oradan alır); kapsam iş bitene kadar açık kalmalı.
        using var scope = Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<TamirhaneDbContext>>();
        await using var db = new TamirhaneDbContext(options, new Staff(shopId));
        await work(db);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private sealed class Staff(Guid shopId) : ICurrentUser
    {
        public Guid ShopId => shopId;
        public Guid StaffId { get; } = Guid.NewGuid();
        public bool IsOwner => true;
    }
}

[CollectionDefinition(Name)]
public sealed class WebCollection : ICollectionFixture<TamirhaneWebFactory>
{
    public const string Name = "web";
}
