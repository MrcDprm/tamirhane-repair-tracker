using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Persistence;

public sealed class TamirhaneDbContext(DbContextOptions<TamirhaneDbContext> options, ICurrentUser currentUser)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Stok tablolarında ShopId domain'de yok; atölyeyi Infrastructure gölge sütunla tutar.</summary>
    internal const string ShopIdColumn = "ShopId";

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    internal DbSet<WorkOrderSequence> WorkOrderSequences => Set<WorkOrderSequence>();

    // Filtre her sorguda bu değeri parametre olarak okur; oturum yoksa Guid.Empty olur ve hiçbir kayıt dönmez.
    private Guid CurrentShopId => currentUser.ShopId;

    Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampShop();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampShop();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TamirhaneDbContext).Assembly);

        // Kiracı filtresi: her sorgu sadece oturumdaki atölyenin kayıtlarını görür.
        // İş emrinin alt kayıtları (geçmiş, kalemler, ödemeler, notlar) sadece iş emri üzerinden yüklenir.
        modelBuilder.Entity<WorkOrder>().HasQueryFilter(o => o.ShopId == CurrentShopId);
        modelBuilder.Entity<Customer>().HasQueryFilter(c => c.ShopId == CurrentShopId);
        modelBuilder.Entity<Device>().HasQueryFilter(d => d.ShopId == CurrentShopId);
        modelBuilder.Entity<Part>().HasQueryFilter(p => p.ShopId == CurrentShopId);
        modelBuilder.Entity<StockLevel>().HasQueryFilter(l => EF.Property<Guid>(l, ShopIdColumn) == CurrentShopId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(m => EF.Property<Guid>(m, ShopIdColumn) == CurrentShopId);
    }

    /// <summary>
    /// Yeni kayıtların atölyesi her zaman oturumdan gelir. Stok kayıtlarına burada yazılır;
    /// ShopId'yi kendisi taşıyan kayıtlar başka atölyeye aitse kayıt hiç yapılmaz.
    /// </summary>
    private void StampShop()
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is StockLevel or StockMovement)
            {
                entry.Property(ShopIdColumn).CurrentValue = CurrentShopId;
                continue;
            }

            var shopId = entry.Entity switch
            {
                WorkOrder o => o.ShopId,
                Customer c => c.ShopId,
                Device d => d.ShopId,
                Part p => p.ShopId,
                _ => CurrentShopId,
            };
            if (shopId != CurrentShopId)
                throw new InvalidOperationException($"{entry.Entity.GetType().Name} başka bir atölyeye yazılmak istendi.");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // PostgreSQL timestamptz sadece UTC kabul eder; Türkiye saati (+03:00) yazarken UTC'ye çevrilir.
        builder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();
        builder.Properties<DateTimeOffset?>().HaveConversion<UtcConverter>();
        // Durum, tür gibi enum'lar veritabanında okunabilir metin olarak durur.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<string>().HaveMaxLength(1000);
    }

    private sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        value => value.ToUniversalTime(),
        value => value);
}

/// <summary>Atölye ve yıl başına iş emri sayacı (sadece Infrastructure'da; domain'in kavramı değil).</summary>
internal sealed class WorkOrderSequence
{
    public Guid ShopId { get; set; }
    public int Year { get; set; }
    public int Value { get; set; }
}
