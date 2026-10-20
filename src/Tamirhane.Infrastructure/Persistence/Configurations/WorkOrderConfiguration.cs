using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).ValueGeneratedNever();
        b.Property(o => o.Number).HasMaxLength(20);
        b.Property(o => o.TrackingCode).HasMaxLength(TrackingCode.Length).IsFixedLength();
        b.Property(o => o.Complaint).HasMaxLength(WorkOrder.MaxTextLength);

        b.HasIndex(o => new { o.ShopId, o.Number }).IsUnique();
        b.HasIndex(o => o.TrackingCode).IsUnique();
        b.HasIndex(o => new { o.ShopId, o.Status });
        b.HasIndex(o => o.CustomerId);

        // Teklif kararı ayrı tablo değil, iş emri satırındaki sütunlar (owned).
        b.OwnsOne(o => o.QuoteDecision, d =>
        {
            d.Property(x => x.Kind).HasColumnName("quote_decision");
            d.Property(x => x.ByName).HasColumnName("quote_decided_by").HasMaxLength(100);
            d.Property(x => x.At).HasColumnName("quote_decided_at");
            d.Property(x => x.StaffId).HasColumnName("quote_decided_staff_id");
        });

        // Koleksiyonlar domain'de salt okunur; EF özel alanları (_history...) doldurur.
        b.HasMany(o => o.History).WithOne().HasForeignKey(h => h.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(o => o.QuoteLines).WithOne().HasForeignKey(l => l.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(o => o.Payments).WithOne().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(o => o.Notes).WithOne().HasForeignKey(n => n.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        foreach (var navigation in new[] { nameof(WorkOrder.History), nameof(WorkOrder.QuoteLines), nameof(WorkOrder.Payments), nameof(WorkOrder.Notes) })
            b.Navigation(navigation).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class StatusChangeConfiguration : IEntityTypeConfiguration<StatusChange>
{
    public void Configure(EntityTypeBuilder<StatusChange> b)
    {
        b.ToTable("status_changes");
        b.HasKey(h => h.Id);
        b.Property(h => h.Id).ValueGeneratedNever();
        b.HasIndex(h => new { h.WorkOrderId, h.At });
    }
}

internal sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> b)
    {
        b.ToTable("quote_lines");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property(l => l.Description).HasMaxLength(QuoteLine.MaxDescriptionLength);
        b.Property(l => l.Quantity).HasPrecision(10, 2);
        b.Property(l => l.VatRate).HasPrecision(5, 2);
        b.Property(l => l.DiscountRate).HasPrecision(5, 2);
        b.HasIndex(l => l.PartId);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Note).HasMaxLength(Payment.MaxNoteLength);
    }
}

internal sealed class WorkOrderNoteConfiguration : IEntityTypeConfiguration<WorkOrderNote>
{
    public void Configure(EntityTypeBuilder<WorkOrderNote> b)
    {
        b.ToTable("work_order_notes");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).ValueGeneratedNever();
    }
}

internal sealed class WorkOrderSequenceConfiguration : IEntityTypeConfiguration<WorkOrderSequence>
{
    public void Configure(EntityTypeBuilder<WorkOrderSequence> b) => b.HasKey(s => new { s.ShopId, s.Year });
}
