using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Demo;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Demo atölyelerin kaydı (#36). Oturumsuz çalışır: kiracı filtresi bilerek kapalı, sorgular IsDemo ile süzülür.
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
}
