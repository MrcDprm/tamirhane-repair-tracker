using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Demo;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Demo atölyelerin kaydı (#36). Oturumsuz çalışır (demo açılışı, arka plandaki temizlik): kiracı filtresi bilerek
/// kapalı, sorgular IsDemo ve atölye kimliğiyle süzülür.
/// </summary>
internal sealed class DemoShops(TamirhaneDbContext db) : IDemoShops
{
    public Task<int> CountOpenedSinceAsync(DateTimeOffset since, CancellationToken ct) =>
        db.Shops.IgnoreQueryFilters().CountAsync(s => s.IsDemo && s.CreatedAt > since, ct);

    public void Add(DemoWorkshop w)
    {
        db.Shops.Add(w.Shop);
        db.Branches.Add(w.Branch);
        db.Staff.AddRange(w.Staff);
        db.Customers.AddRange(w.Customers);
        db.Devices.AddRange(w.Devices);
        db.Parts.AddRange(w.Parts);
        db.WorkOrders.AddRange(w.WorkOrders);
        // Stok kayıtlarında atölye gölge sütunda; oturum olmadığı için eklendikten sonra burada yazılır
        // (StampShop, bu kayıtta eklenen yeni atölyeninkini korur).
        db.StockLevels.AddRange(w.StockLevels);
        db.StockMovements.AddRange(w.StockMovements);
        foreach (var level in w.StockLevels)
            db.Entry(level).Property(TamirhaneDbContext.ShopIdColumn).CurrentValue = w.Shop.Id;
        foreach (var movement in w.StockMovements)
            db.Entry(movement).Property(TamirhaneDbContext.ShopIdColumn).CurrentValue = w.Shop.Id;
        // Sayaç örnek iş emirlerinin ardından devam etsin: ziyaretçinin açtığı ilk iş emri sıradaki numarayı alır.
        db.WorkOrderSequences.AddRange(w.Sequences.Select(s => new WorkOrderSequence { ShopId = w.Shop.Id, Year = s.Key, Value = s.Value }));
    }

    public async Task<IReadOnlyList<Guid>> ListOpenedBeforeAsync(DateTimeOffset before, int take, CancellationToken ct) =>
        await db.Shops.IgnoreQueryFilters()
            .Where(s => s.IsDemo && s.CreatedAt < before)
            .OrderBy(s => s.CreatedAt)
            .Select(s => s.Id)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>
    /// Toplu SQL silmeleri (kayıtlar belleğe yüklenmez). Sıra yabancı anahtarlara göre: iş emrinin alt kayıtları, cihazlar
    /// ve stok seviyeleri veritabanında zincirleme silinir; personel şubeye "restrict" ile bağlı olduğu için şubeden önce.
    /// Hesaplar en sonda: personel kaydı gidince hangi hesabın bu atölyenin olduğu bilinemezdi.
    /// </summary>
    public async Task DeleteAsync(Guid shopId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Shops.IgnoreQueryFilters().AnyAsync(s => s.Id == shopId && s.IsDemo, ct))
            throw new InvalidOperationException("Sadece demo atölye silinebilir.");

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
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }
}
