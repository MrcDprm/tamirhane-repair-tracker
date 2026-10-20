using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Inventory;

public enum StockMovementReason
{
    /// <summary>Tedarikçiden gelen parça.</summary>
    Purchase,
    /// <summary>Onarımda kullanıldı (iş emri hazır olduğunda).</summary>
    Used,
    /// <summary>İptal edilen iş emrinden stoğa geri döndü.</summary>
    Returned,
    /// <summary>Sayım farkı, kırılan parça vb. elle düzeltme.</summary>
    Adjustment,
}

/// <summary>Stoktaki her değişikliğin kaydı. Miktar hiçbir zaman kayıtsız değişmez.</summary>
public sealed class StockMovement : Entity
{
    private StockMovement() { }

    internal StockMovement(Guid partId, Guid branchId, int change, StockMovementReason reason,
        Guid? workOrderId, Guid staffId, DateTimeOffset at, string? note)
    {
        PartId = partId;
        BranchId = branchId;
        Change = change;
        Reason = reason;
        WorkOrderId = workOrderId;
        StaffId = staffId;
        At = at;
        Note = note;
    }

    public Guid PartId { get; private set; }
    public Guid BranchId { get; private set; }
    /// <summary>Artı giriş, eksi çıkış.</summary>
    public int Change { get; private set; }
    public StockMovementReason Reason { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public Guid StaffId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Note { get; private set; }
}
