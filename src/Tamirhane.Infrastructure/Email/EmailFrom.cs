using System.Net.Mail;
using System.Text;

namespace Tamirhane.Infrastructure.Email;

/// <summary>
/// Gönderen: görünen ad + adres. Adres sabit (Resend'de doğrulanmış alan adı), ad e-postaya göre değişebilir
/// (müşteri bildirimlerinde atölyenin adı). Türkçe harfler olduğu gibi JSON'a girer; başlık kodlamasını
/// (RFC 2047) Resend yapar.
/// </summary>
public sealed record EmailFrom(string Name, string Address)
{
    public const int MaxNameLength = 70;

    /// <summary>EMAIL_FROM ayarı ("Tamirhane &lt;tamirhane@alan.adi&gt;"); bozuksa uygulama açılmaz.</summary>
    public static EmailFrom Parse(string value)
    {
        if (!MailAddress.TryCreate(value, out var parsed) || string.IsNullOrWhiteSpace(parsed.DisplayName))
            throw new InvalidOperationException("EMAIL_FROM geçersiz: \"Ad <adres@alan.adi>\" biçiminde olmalı.");
        return new EmailFrom(Clean(parsed.DisplayName) ?? "Tamirhane", parsed.Address);
    }

    /// <summary>
    /// "From" başlığının değeri. Ad tırnak içinde yazılır; satır sonu, tırnak, ters bölü ve köşeli parantez atılır:
    /// atölye adına "Kaya\r\nBcc: ..." yazılarak e-postaya başlık eklenemez, adres de değiştirilemez.
    /// </summary>
    public string Format(string? displayName = null) => $"\"{Clean(displayName) ?? Name}\" <{Address}>";

    private static string? Clean(string? name)
    {
        if (name is null)
            return null;
        var kept = new StringBuilder(name.Length);
        foreach (var c in name)
            kept.Append(char.IsControl(c) || c is '"' or '\\' or '<' or '>' ? ' ' : c);
        var cleaned = string.Join(' ', kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length > MaxNameLength)
            cleaned = cleaned[..MaxNameLength].TrimEnd();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
