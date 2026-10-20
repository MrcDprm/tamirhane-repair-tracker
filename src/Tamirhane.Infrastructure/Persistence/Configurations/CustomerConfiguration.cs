using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Name).HasMaxLength(Customer.MaxNameLength);
        b.Property(c => c.Email).HasMaxLength(Customer.MaxEmailLength);
        // Telefon değer nesnesi veritabanında E.164 metni olarak durur; okurken yeniden doğrulanır.
        b.Property(c => c.Phone)
            .HasConversion(p => p.Value, v => PhoneNumber.Parse(v).Value)
            .HasMaxLength(16);
        b.HasIndex(c => new { c.ShopId, c.Phone }).IsUnique();
    }
}

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.Brand).HasMaxLength(Device.MaxBrandLength);
        b.Property(d => d.Model).HasMaxLength(Device.MaxModelLength);
        b.Property(d => d.Serial).HasMaxLength(Device.MaxSerialLength);
        b.Property(d => d.Imei).HasMaxLength(15).IsFixedLength();
        b.HasOne<Customer>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(d => new { d.ShopId, d.Imei });
    }
}
