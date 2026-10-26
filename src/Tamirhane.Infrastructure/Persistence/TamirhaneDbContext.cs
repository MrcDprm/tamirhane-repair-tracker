using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın tek veritabanı bağlamı. Identity'nin kullanıcı tabloları da burada (rol tabloları yok:
/// rol domain'deki Staff'ta), böylece hesap ve atölye aynı transaction'da kaydedilebilir.
/// </summary>
public sealed class TamirhaneDbContext(DbContextOptions<TamirhaneDbContext> options, ICurrentUser currentUser)
    : IdentityUserContext<ApplicationUser, Guid>(options), IUnitOfWork, IDataProtectionKeyContext
{
    /// <summary>Stok tablolarında ShopId domain'de yok; atölyeyi Infrastructure gölge sütunla tutar.</summary>
    internal const string ShopIdColumn = "ShopId";

    /// <summary>Çerez ve e-posta token'larını imzalayan anahtarlar (Data Protection).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    internal DbSet<WorkOrderSequence> WorkOrderSequences => Set<WorkOrderSequence>();

    // Filtre her sorguda bu değeri parametre olarak okur; oturum yoksa Guid.Empty olur ve hiçbir kayıt dönmez.
    private Guid CurrentShopId => currentUser.ShopId;

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new ConcurrencyConflictException(e);
        }
    }

    async Task<ITransaction> IUnitOfWork.BeginTransactionAsync(CancellationToken ct) =>
        new EfTransaction(await Database.BeginTransactionAsync(ct));

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
        base.OnModelCreating(modelBuilder); // Identity tabloları
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TamirhaneDbContext).Assembly);

        // Kiracı filtresi: her sorgu sadece oturumdaki atölyenin kayıtlarını görür.
        // İş emrinin alt kayıtları (geçmiş, kalemler, ödemeler, notlar) sadece iş emri üzerinden yüklenir.
        modelBuilder.Entity<Shop>().HasQueryFilter(s => s.Id == CurrentShopId);
        modelBuilder.Entity<Staff>().HasQueryFilter(s => s.ShopId == CurrentShopId);
        modelBuilder.Entity<Branch>().HasQueryFilter(b => b.ShopId == CurrentShopId);
        modelBuilder.Entity<Invitation>().HasQueryFilter(i => i.ShopId == CurrentShopId);
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
    /// Atölye kaydı oturum açılmadan yapılır: aynı kayıtta eklenen yeni atölyenin kendisine, ilk şubesine ve sahibine izin verilir.
    /// Davet kabulü de oturumsuzdur: aynı kayıtta kabul edilen davetin atölyesine sadece personel eklenebilir.
    /// </summary>
    private void StampShop()
    {
        var newShops = ChangeTracker.Entries<Shop>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Id)
            .ToHashSet();
        var acceptedInvitationShops = ChangeTracker.Entries<Invitation>()
            .Where(e => e.State == EntityState.Modified && e.Property(i => i.AcceptedAt).IsModified && e.Entity.AcceptedAt is not null)
            .Select(e => e.Entity.ShopId)
            .ToHashSet();

        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is StockLevel or StockMovement)
            {
                entry.Property(ShopIdColumn).CurrentValue = CurrentShopId;
                continue;
            }

            var shopId = entry.Entity switch
            {
                Shop s => s.Id,
                Staff s => s.ShopId,
                Branch b => b.ShopId,
                Invitation i => i.ShopId,
                WorkOrder o => o.ShopId,
                Customer c => c.ShopId,
                Device d => d.ShopId,
                Part p => p.ShopId,
                _ => CurrentShopId,
            };
            if (entry.Entity is Staff && acceptedInvitationShops.Contains(shopId))
                continue;
            if (shopId != CurrentShopId && !newShops.Contains(shopId))
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

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

        // Commit edilmeden kapatılan transaction geri alınır.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
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
