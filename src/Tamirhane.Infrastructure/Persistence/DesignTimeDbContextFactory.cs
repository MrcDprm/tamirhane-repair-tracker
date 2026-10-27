using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tamirhane.Application.Abstractions;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// "dotnet ef" komutlarının (migrations add, database update) DbContext'i kurması için. Bağlantı DATABASE_URL
/// ortam değişkeninden, yoksa repodaki .env'den, o da yoksa yerel Docker'daki geliştirme veritabanından (gerçek şifre içermez).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TamirhaneDbContext>
{
    private const string LocalDevelopment = "Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane";

    public TamirhaneDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TamirhaneDbContext>();
        // Önce gerçek ortam değişkeni, sonra repodaki .env (uygulamayla aynı veritabanına bağlansın), sonra yerel varsayılan.
        var connection = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? DotEnvFile.Read(Directory.GetCurrentDirectory()).GetValueOrDefault("DATABASE_URL")
            ?? LocalDevelopment;
        DependencyInjection.Configure(options, connection);
        return new TamirhaneDbContext(options.Options, new NoUser());
    }

    // Migration üretirken oturum yok; filtre sadece modele yazılır, sorgu çalışmaz.
    private sealed class NoUser : ICurrentUser
    {
        public Guid ShopId => Guid.Empty;
        public Guid StaffId => Guid.Empty;
        public Guid BranchId => Guid.Empty;
        public bool IsOwner => false;
    }
}
