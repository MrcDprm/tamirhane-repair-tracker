using System.Buffers.Text;
using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// Müşterinin tarayıcısının bu iş emri için verdiği bildirim aboneliği (Web Push). Tarayıcı üç şey verir:
/// bildirimin gönderileceği adres (push servisinde) ve bildirimi şifrelemek için iki anahtar.
/// İş emrinin alt kaydıdır: takip sayfasından oturumsuz eklenir, iş emriyle birlikte silinir.
/// </summary>
public sealed class PushSubscription : Entity
{
    public const int MaxEndpointLength = 2048;
    private const int PublicKeyBytes = 65; // sıkıştırılmamış P-256 noktası: 0x04 + 32 bayt x + 32 bayt y
    private const int AuthSecretBytes = 16;

    /// <summary>
    /// Sunucu bu adrese istek atacak. Herhangi bir adres kabul edilseydi biri iç ağdaki bir servisi "abonelik" diye
    /// kaydedip sunucuya istek attırabilirdi (SSRF). Bu yüzden sadece tarayıcıların kullandığı push servisleri:
    /// Chrome/Edge (Android)/Opera FCM, Firefox Mozilla, Edge (Windows) WNS, Safari Apple.
    /// </summary>
    private static readonly string[] PushHosts = ["fcm.googleapis.com", "updates.push.services.mozilla.com", "web.push.apple.com"];
    private static readonly string[] PushHostSuffixes = [".notify.windows.com", ".push.apple.com"];

    private PushSubscription() { }

    private PushSubscription(Guid workOrderId, string endpoint, string p256dh, string auth, DateTimeOffset now)
    {
        WorkOrderId = workOrderId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        CreatedAt = now;
    }

    public Guid WorkOrderId { get; private set; }
    public string Endpoint { get; private set; } = "";
    /// <summary>Tarayıcının açık anahtarı (base64url); bildirimi sadece o tarayıcı açabilsin diye şifrelemede kullanılır.</summary>
    public string P256dh { get; private set; } = "";
    /// <summary>Tarayıcının paylaştığı gizli değer (base64url, 16 bayt); şifrelemenin bir parçası.</summary>
    public string Auth { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    internal static Result<PushSubscription> Create(Guid workOrderId, string? endpoint, string? p256dh, string? auth, DateTimeOffset now)
    {
        endpoint = endpoint?.Trim() ?? "";
        // Tarayıcı base64url verir; bazıları sondaki "=" dolgusunu da ekler. Dolgusuz saklanır.
        p256dh = p256dh?.Trim().TrimEnd('=') ?? "";
        auth = auth?.Trim().TrimEnd('=') ?? "";
        if (!IsPushServiceUrl(endpoint) || !IsKey(p256dh, PublicKeyBytes, mustStartWith: 0x04) || !IsKey(auth, AuthSecretBytes))
            return WorkOrderErrors.InvalidPushSubscription;
        return new PushSubscription(workOrderId, endpoint, p256dh, auth, now);
    }

    /// <summary>Aynı tarayıcı yeniden abone olunca anahtarları yenilenmiş olabilir.</summary>
    internal void Renew(PushSubscription fresh)
    {
        P256dh = fresh.P256dh;
        Auth = fresh.Auth;
    }

    public static bool IsPushServiceUrl(string endpoint)
    {
        if (endpoint.Length > MaxEndpointLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            return false;
        var host = uri.IdnHost.ToLowerInvariant();
        return PushHosts.Contains(host) || PushHostSuffixes.Any(host.EndsWith);
    }

    private static bool IsKey(string value, int bytes, byte? mustStartWith = null)
    {
        if (value.Length == 0 || !Base64Url.IsValid(value, out var length) || length != bytes)
            return false;
        return mustStartWith is null || Base64Url.DecodeFromChars(value)[0] == mustStartWith;
    }
}
