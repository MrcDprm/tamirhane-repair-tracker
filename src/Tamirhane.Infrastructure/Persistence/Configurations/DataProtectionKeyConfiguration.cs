using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tamirhane.Infrastructure.Persistence.Configurations;

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    // Anahtar XML'i ~900 karakter, diskte şifreleme açılırsa daha uzun: genel 1000 karakter sınırı yerine sınırsız metin.
    public void Configure(EntityTypeBuilder<DataProtectionKey> b) => b.Property(k => k.Xml).HasColumnType("text");
}
