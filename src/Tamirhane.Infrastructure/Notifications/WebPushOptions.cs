using System.Buffers.Text;
using Tamirhane.Application.Abstractions;

namespace Tamirhane.Infrastructure.Notifications;

/// <summary>
/// Tarayıcı bildirimi (Web Push) için VAPID anahtar çifti: sunucu, bildirimi gönderenin kendisi olduğunu push servisine
/// bu anahtarla imzalayarak kanıtlar. Açık anahtar tarayıcıya da verilir (abone olurken); gizli anahtar sadece sunucuda.
/// Subject, push servisinin sorun olursa ulaşacağı adres (mailto: ya da https:).
/// </summary>
public sealed record WebPushOptions(string PublicKey, string PrivateKey, string Subject)
{
    /// <summary>
    /// Ortam değişkenlerinden okur. Üçü de boşsa özellik kapalıdır (null). Bir kısmı doluysa ya da anahtarlar bozuksa
    /// uygulama açılırken durur: yarım ayar yayında sessizce "bildirim gitmiyor" olarak ortaya çıkmasın.
    /// </summary>
    public static WebPushOptions? From(string? publicKey, string? privateKey, string? subject)
    {
        if (string.IsNullOrWhiteSpace(publicKey) && string.IsNullOrWhiteSpace(privateKey) && string.IsNullOrWhiteSpace(subject))
            return null;

        publicKey = publicKey?.Trim().TrimEnd('=') ?? "";
        privateKey = privateKey?.Trim().TrimEnd('=') ?? "";
        subject = subject?.Trim() ?? "";
        if (!IsKey(publicKey, 65) || Base64Url.DecodeFromChars(publicKey)[0] != 0x04)
            throw new InvalidOperationException("VAPID_PUBLIC_KEY geçersiz: base64url, 65 baytlık P-256 açık anahtarı olmalı.");
        if (!IsKey(privateKey, 32))
            throw new InvalidOperationException("VAPID_PRIVATE_KEY geçersiz: base64url, 32 baytlık P-256 gizli anahtarı olmalı.");
        if (!subject.StartsWith("mailto:", StringComparison.Ordinal) && !subject.StartsWith("https://", StringComparison.Ordinal))
            throw new InvalidOperationException("VAPID_SUBJECT \"mailto:\" ya da \"https://\" ile başlamalı.");
        return new WebPushOptions(publicKey, privateKey, subject);
    }

    private static bool IsKey(string value, int bytes) =>
        value.Length > 0 && Base64Url.IsValid(value, out var length) && length == bytes;

    // Gizli anahtar loglara ya da hata sayfasına düşmesin (record'un ToString'i bütün alanları yazardı).
    public override string ToString() => $"WebPushOptions {{ PublicKey = {PublicKey}, Subject = {Subject} }}";
}

/// <summary><see cref="IPushSettings"/>: ayar yoksa açık anahtar boş (özellik kapalı).</summary>
internal sealed class PushSettings(string? publicKey) : IPushSettings
{
    public string? PublicKey { get; } = publicKey;
}
