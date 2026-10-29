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
        b.Property(s => s.IsDemo);
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
        b.HasOne<Branch>().WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(Branch.MaxNameLength);
        b.Property(x => x.Address).HasMaxLength(Branch.MaxAddressLength);
        b.Property(x => x.Phone).HasMaxLength(16);
        b.HasIndex(x => new { x.ShopId, x.CreatedAt });
        b.HasOne<Shop>().WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
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
