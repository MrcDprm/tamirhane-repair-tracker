using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Inventory;

/// <summary>Bir parçanın bir şubedeki miktarı. Her değişiklik bir <see cref="StockMovement"/> üretir.</summary>
public sealed class StockLevel : Entity
{
    public const int MaxQuantity = 1_000_000;
    public const int MaxNoteLength = 200;

    private StockLevel() { }

    public Guid PartId { get; private set; }
    public Guid BranchId { get; private set; }
    public int Quantity { get; private set; }

    public static StockLevel Create(Guid partId, Guid branchId) =>
        new() { PartId = partId, BranchId = branchId, Quantity = 0 };

    public Result<StockMovement> Receive(int quantity, Guid staffId, DateTimeOffset now, string? note)
    {
        if (quantity <= 0 || Quantity + (long)quantity > MaxQuantity)
            return InventoryErrors.InvalidQuantity;
        if (note?.Length > MaxNoteLength)
            return InventoryErrors.NoteTooLong;

        return Apply(quantity, StockMovementReason.Purchase, null, staffId, now, Clean(note));
    }

    /// <summary>Sayım sonucuna göre miktarı düzeltir; sebep zorunlu (ör. "Sayım farkı", "Kırıldı").</summary>
    public Result<StockMovement> Adjust(int newQuantity, Guid staffId, DateTimeOffset now, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (newQuantity < 0 || newQuantity > MaxQuantity)
            return InventoryErrors.InvalidQuantity;
        if (reason.Length == 0 || reason.Length > MaxNoteLength)
            return InventoryErrors.ReasonRequired;
        if (newQuantity == Quantity)
            return InventoryErrors.InvalidQuantity;

        return Apply(newQuantity - Quantity, StockMovementReason.Adjustment, null, staffId, now, reason);
    }

    internal StockMovement Apply(int change, StockMovementReason reason, Guid? workOrderId, Guid staffId, DateTimeOffset now, string? note)
    {
        Quantity += change;
        return new StockMovement(PartId, BranchId, change, reason, workOrderId, staffId, now, note);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
