using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure.Tests.Identity;

/// <summary>Cihaz şifresinin şifrelenmesi ve Neon bağlantısının sertifika doğrulaması. Docker gerekmez.</summary>
public sealed class PasscodeProtectorTests
{
    private static PasscodeProtector Protector(string keysDirectory) =>
        new(DataProtectionProvider.Create(new DirectoryInfo(keysDirectory), b => b.SetApplicationName("Tamirhane")));

    [Fact]
    public void Passcode_is_stored_encrypted_and_read_back()
    {
        var protector = Protector(Directory.CreateTempSubdirectory().FullName);

        var stored = protector.Protect("PIN: 4826");

        Assert.StartsWith("dp1:", stored);
        Assert.DoesNotContain("4826", stored);
        Assert.Equal("PIN: 4826", protector.Unprotect(stored));
    }

    [Fact]
    public void Plain_value_saved_before_encryption_is_still_readable()
    {
        Assert.Equal("1234", Protector(Directory.CreateTempSubdirectory().FullName).Unprotect("1234"));
    }

    [Fact]
    public void Another_key_ring_cannot_read_it()
    {
        var stored = Protector(Directory.CreateTempSubdirectory().FullName).Protect("PIN: 4826");

        // Anahtar kaybolduysa ya da veritabanı başka bir yere taşındıysa sayfa çökmez, şifre okunamadı denir.
        Assert.Null(Protector(Directory.CreateTempSubdirectory().FullName).Unprotect(stored));
    }

    [Theory]
    [InlineData("postgresql://u:p@ep-x.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require", SslMode.VerifyFull)]
    [InlineData("postgresql://u:p@localhost:5433/tamirhane", SslMode.Prefer)]
    [InlineData("postgresql://u:p@db.example/x?sslmode=verify-ca", SslMode.VerifyCA)]
    public void Required_tls_also_verifies_the_server_certificate(string url, SslMode expected)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionStrings.Normalize(url));

        Assert.Equal(expected, builder.SslMode);
    }
}
