using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Persistence.Configurations;

internal sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Name).HasMaxLength(Shop.MaxNameLength);
        b.Property(s => s.Locale).HasMaxLength(5);
    }
}

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.DisplayName).HasMaxLength(Staff.MaxDisplayNameLength);
        b.HasOne<Shop>().WithMany().HasForeignKey(s => s.ShopId).OnDelete(DeleteBehavior.Cascade);
        // Bir hesap tek atölyede çalışır; girişte personel kaydı hesaptan bulunur.
        // Hesap silinince personel kaydı kalır (iş emri geçmişindeki adı için), bağ boşalır.
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(s => s.UserId).IsUnique();
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Email).HasMaxLength(Invitation.MaxEmailLength);
        b.Property(i => i.TokenHash).HasMaxLength(32);
        b.HasIndex(i => i.TokenHash).IsUnique(); // linkle gelen arama bu indeksten
        b.HasIndex(i => new { i.ShopId, i.Email });
        b.HasOne<Shop>().WithMany().HasForeignKey(i => i.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}
