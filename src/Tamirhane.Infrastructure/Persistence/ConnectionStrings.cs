using Npgsql;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Neon ve Render bağlantıyı URL olarak verir (postgresql://kullanıcı:şifre@sunucu/veritabanı?sslmode=require);
/// Npgsql ise "Host=...;Username=..." biçimini ister. İkisi de kabul edilir.
/// </summary>
public static class ConnectionStrings
{
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(kv[1], ignoreCase: true, out var mode))
                builder.SslMode = mode;
        }
        return builder.ConnectionString;
    }
}
