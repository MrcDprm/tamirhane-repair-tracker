using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// "dotnet ef migrations add" komutunun DbContext'i kurması için. Bağlantı DATABASE_URL ortam
/// değişkeninden; yoksa yerel Docker'daki geliştirme veritabanı (gerçek şifre içermez).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TamirhaneDbContext>
{
    private const string LocalDevelopment = "Host=localhost;Port=5432;Database=tamirhane;Username=tamirhane;Password=tamirhane";

    public TamirhaneDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TamirhaneDbContext>();
        DependencyInjection.Configure(options, Environment.GetEnvironmentVariable("DATABASE_URL") ?? LocalDevelopment);
        return new TamirhaneDbContext(options.Options);
    }
}
