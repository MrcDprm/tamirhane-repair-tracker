using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Shops;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Atölye silme: sahibin kendi atölyesini silmesi ve süresi dolan demolar aynı sorguları kullanır.
/// Kiracı filtresi bilerek kapalı; her sorgu atölye kimliğiyle süzülür.
/// </summary>
internal sealed class ShopEraser(TamirhaneDbContext db) : IShopEraser
{
    public async Task DeleteAsync(Guid shopId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await DeleteAllAsync(db, shopId, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Toplu SQL silmeleri (kayıtlar belleğe yüklenmez); transaction çağıranın. Sıra yabancı anahtarlara göre: iş emrinin
    /// alt kayıtları, cihazlar ve stok seviyeleri veritabanında zincirleme silinir; personel şubeye "restrict" ile bağlı
    /// olduğu için şubeden önce. Hesaplar en sonda: personel kaydı gidince hangi hesabın bu atölyenin olduğu bilinemezdi.
    /// </summary>
    internal static async Task DeleteAllAsync(TamirhaneDbContext db, Guid shopId, CancellationToken ct)
    {
        var userIds = await db.Staff.IgnoreQueryFilters()
            .Where(s => s.ShopId == shopId && s.UserId != null)
            .Select(s => s.UserId!.Value)
            .ToListAsync(ct);

        await db.WorkOrders.IgnoreQueryFilters().Where(o => o.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.StockMovements.IgnoreQueryFilters()
            .Where(m => EF.Property<Guid>(m, TamirhaneDbContext.ShopIdColumn) == shopId).ExecuteDeleteAsync(ct);
        await db.Parts.IgnoreQueryFilters().Where(p => p.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Customers.IgnoreQueryFilters().Where(c => c.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Invitations.IgnoreQueryFilters().Where(i => i.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Staff.IgnoreQueryFilters().Where(s => s.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Branches.IgnoreQueryFilters().Where(b => b.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.WorkOrderSequences.Where(s => s.ShopId == shopId).ExecuteDeleteAsync(ct);
        await db.Shops.IgnoreQueryFilters().Where(s => s.Id == shopId).ExecuteDeleteAsync(ct);
        await OAuthGrants.DeleteForUsersAsync(db, userIds, ct);
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(ct);
    }
}
