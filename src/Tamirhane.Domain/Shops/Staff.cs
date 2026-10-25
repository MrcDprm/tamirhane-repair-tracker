using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Shops;

public enum StaffRole
{
    /// <summary>Her şey: ayarlar, personel, ciro.</summary>
    Owner,
    /// <summary>İş emri ve stok kullanımı; ayarlar, personel ve ciro yok.</summary>
    Technician,
}

/// <summary>
/// Atölyede çalışan kişi. Giriş bilgileri (e-posta, şifre) Identity'de durur;
/// domain kullanıcıyı sadece UserId ile bilir.
/// </summary>
public sealed class Staff : Entity
{
    public const int MaxDisplayNameLength = 100;

    private Staff() { }

    public Guid ShopId { get; private set; }
    public Guid UserId { get; private set; }
    public StaffRole Role { get; private set; }
    public string DisplayName { get; private set; } = "";

    public bool IsOwner => Role == StaffRole.Owner;

    public static Result<Staff> Create(Guid shopId, Guid userId, StaffRole role, string displayName)
    {
        displayName = string.Join(' ', (displayName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length < 2 || displayName.Length > MaxDisplayNameLength)
            return ShopErrors.InvalidDisplayName;
        if (!Enum.IsDefined(role))
            return ShopErrors.InvalidRole;

        return new Staff { ShopId = shopId, UserId = userId, Role = role, DisplayName = displayName };
    }
}
