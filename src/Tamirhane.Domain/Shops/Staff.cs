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
/// Çıkarılan personelin kaydı silinmez (iş emri geçmişinde adı görünmeye devam eder), hesapla bağı kopar.
/// </summary>
public sealed class Staff : Entity
{
    public const int MaxDisplayNameLength = 100;

    private Staff() { }

    public Guid ShopId { get; private set; }
    /// <summary>Çıkarılan personelde boş: hesabı silinmiştir.</summary>
    public Guid? UserId { get; private set; }
    public StaffRole Role { get; private set; }
    public string DisplayName { get; private set; } = "";
    /// <summary>Çalıştığı şube: yeni iş emirleri ve stok işlemleri buraya yazılır.</summary>
    public Guid BranchId { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }

    public bool IsOwner => Role == StaffRole.Owner;
    public bool IsActive => RemovedAt is null;

    public static Result<Staff> Create(Guid shopId, Guid userId, StaffRole role, string displayName, Guid branchId)
    {
        displayName = string.Join(' ', (displayName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length < 2 || displayName.Length > MaxDisplayNameLength)
            return ShopErrors.InvalidDisplayName;
        if (!Enum.IsDefined(role))
            return ShopErrors.InvalidRole;

        return new Staff { ShopId = shopId, UserId = userId, Role = role, DisplayName = displayName, BranchId = branchId };
    }

    /// <summary>Çalıştığı şubeyi değiştirir (birden çok şubesi olan atölyede menüden).</summary>
    public Result MoveTo(Branch branch)
    {
        if (branch.ShopId != ShopId)
            return ShopErrors.BranchNotFound;
        BranchId = branch.Id;
        return Result.Success();
    }

    /// <summary>
    /// Personeli atölyeden çıkarır. Sahip çıkarılamaz: atölyenin tek sahibi o, kendini çıkarırsa atölye sahipsiz kalır.
    /// </summary>
    public Result Remove(DateTimeOffset now)
    {
        if (IsOwner)
            return ShopErrors.CannotRemoveOwner;
        if (!IsActive)
            return ShopErrors.StaffNotFound;

        RemovedAt = now;
        UserId = null;
        return Result.Success();
    }
}
