using System.Buffers.Text;
using System.Security.Cryptography;
using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Shops;

/// <summary>
/// Sahibin bir teknisyene gönderdiği davet. Link içindeki token veritabanına yazılmaz, sadece SHA-256 özeti durur:
/// veritabanı sızsa bile bekleyen davetler kullanılamaz. Davet 7 gün geçerli ve tek kullanımlıktır.
/// </summary>
public sealed class Invitation : Entity
{
    public const int MaxEmailLength = 256;
    public const int TokenBytes = 32;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation() { }

    public Guid ShopId { get; private set; }
    /// <summary>Küçük harfe çevrilmiş adres; aynı kişiye ikinci davet eskisini geçersiz kılar.</summary>
    public string Email { get; private set; } = "";
    public byte[] TokenHash { get; private set; } = [];
    public Guid InvitedByStaffId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsPending(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    /// <summary>Daveti ve linke konacak token'ı üretir. Token sadece bu dönüşte vardır; bir daha elde edilemez.</summary>
    public static Result<(Invitation Invitation, string Token)> Create(
        Guid shopId, string email, Guid invitedByStaffId, DateTimeOffset now)
    {
        email = NormalizeEmail(email);
        // Adresin gerçekten var olduğunu davet kabulü kanıtlar (link o adrese gider); burada sadece biçim.
        var at = email.IndexOf('@');
        if (email.Length > MaxEmailLength || at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1
            || email.Any(char.IsWhiteSpace))
            return ShopErrors.InvalidEmail;

        var token = RandomNumberGenerator.GetBytes(TokenBytes);
        var invitation = new Invitation
        {
            ShopId = shopId,
            Email = email,
            TokenHash = SHA256.HashData(token),
            InvitedByStaffId = invitedByStaffId,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
        return (invitation, Base64Url.EncodeToString(token));
    }

    /// <summary>Linkteki token'ın veritabanında aranacak özeti; token bozuksa boş.</summary>
    public static byte[]? HashToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || !Base64Url.IsValid(token, out var length) || length != TokenBytes)
            return null;
        return SHA256.HashData(Base64Url.DecodeFromChars(token));
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>Süresi dolmuş, kullanılmış ya da geri alınmış davet aynı hatayı verir: link geçersiz.</summary>
    public Result Accept(DateTimeOffset now)
    {
        if (!IsPending(now))
            return ShopErrors.InvalidInvitation;
        AcceptedAt = now;
        return Result.Success();
    }

    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is null && RevokedAt is null)
            RevokedAt = now;
    }
}
