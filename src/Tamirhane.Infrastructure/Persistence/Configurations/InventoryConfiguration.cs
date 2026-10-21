using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tamirhane.Domain.Inventory;

namespace Tamirhane.Infrastructure.Persistence.Configurations;

internal sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Sku).HasMaxLength(Part.MaxSkuLength);
        b.Property(p => p.Name).HasMaxLength(Part.MaxNameLength);
        b.HasIndex(p => new { p.ShopId, p.Sku }).IsUnique();
    }
}

internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property<Guid>(TamirhaneDbContext.ShopIdColumn);
        b.HasOne<Part>().WithMany().HasForeignKey(l => l.PartId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(l => new { l.PartId, l.BranchId }).IsUnique();
        // İki kişi aynı anda aynı parçayı kullanırsa ikinci kayıt hata alır (iyimser eşzamanlılık).
        b.Property(l => l.Quantity).IsConcurrencyToken();
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property<Guid>(TamirhaneDbContext.ShopIdColumn);
        b.Property(m => m.Note).HasMaxLength(StockLevel.MaxNoteLength);
        b.HasIndex(m => new { m.PartId, m.At });
        b.HasIndex(m => m.WorkOrderId);
    }
}
