using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Inventory;

/// <summary>
/// Bir şubenin stoğu, iş emriyle parça kullanma ve geri alma için. Uygulama katmanı iş emrinin
/// parçalarına ait <see cref="StockLevel"/> kayıtlarını yükleyip bu nesneyle iş emrine verir.
/// </summary>
public sealed class BranchStock
{
    private readonly Dictionary<Guid, StockLevel> _levels;

    public BranchStock(Guid branchId, IEnumerable<StockLevel> levels)
    {
        BranchId = branchId;
        _levels = levels.Where(l => l.BranchId == branchId).ToDictionary(l => l.PartId);
    }

    public Guid BranchId { get; }

    public int QuantityOf(Guid partId) => _levels.TryGetValue(partId, out var level) ? level.Quantity : 0;

    /// <summary>Hepsi ya da hiçbiri: bir parça bile yetmezse hiçbir miktar değişmez.</summary>
    internal Result<IReadOnlyList<StockMovement>> Consume(
        IReadOnlyDictionary<Guid, int> parts, Guid workOrderId, Guid staffId, DateTimeOffset now)
    {
        foreach (var (partId, quantity) in parts)
            if (QuantityOf(partId) < quantity)
                return InventoryErrors.InsufficientStock;

        return parts
            .Select(p => _levels[p.Key].Apply(-p.Value, StockMovementReason.Used, workOrderId, staffId, now, null))
            .ToList();
    }

    internal IReadOnlyList<StockMovement> Return(
        IReadOnlyDictionary<Guid, int> parts, Guid workOrderId, Guid staffId, DateTimeOffset now)
    {
        // Kullanılan her parçanın kaydı kullanılırken vardı; yoksa uygulama katmanı eksik yüklemiştir.
        foreach (var partId in parts.Keys)
            if (!_levels.ContainsKey(partId))
                throw new InvalidOperationException($"Stok kaydı yüklenmedi: {partId}");

        return parts
            .Select(p => _levels[p.Key].Apply(p.Value, StockMovementReason.Returned, workOrderId, staffId, now, null))
            .ToList();
    }
}
