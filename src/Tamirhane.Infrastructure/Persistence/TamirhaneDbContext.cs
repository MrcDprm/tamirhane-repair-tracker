using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Persistence;

public sealed class TamirhaneDbContext(DbContextOptions<TamirhaneDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    internal DbSet<WorkOrderSequence> WorkOrderSequences => Set<WorkOrderSequence>();

    Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TamirhaneDbContext).Assembly);

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
