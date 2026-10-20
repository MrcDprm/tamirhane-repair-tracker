using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Infrastructure.Persistence;

internal sealed class WorkOrderRepository(TamirhaneDbContext db) : IWorkOrderRepository
{
    // Bir iş emri her zaman parçalarıyla birlikte yüklenir; domain kuralları eksik veriyle çalışmasın.
    private IQueryable<WorkOrder> Full => db.WorkOrders
        .Include(o => o.History)
        .Include(o => o.QuoteLines)
        .Include(o => o.Payments)
        .Include(o => o.Notes)
        .AsSplitQuery();

    public Task<WorkOrder?> GetAsync(Guid id, CancellationToken ct) =>
        Full.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<WorkOrder?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct) =>
        Full.FirstOrDefaultAsync(o => o.TrackingCode == trackingCode, ct);

    /// <summary>
    /// Sayaç tek SQL komutuyla artar: aynı anda açılan iki iş emri aynı numarayı alamaz.
    /// Komut kaydın kendisinden ayrı çalışır; kayıt başarısız olursa o numara atlanır (kabul edilen bedel).
    /// </summary>
    public async Task<int> NextSequenceAsync(Guid shopId, int year, CancellationToken ct)
    {
        var values = await db.Database.SqlQuery<int>($"""
            INSERT INTO work_order_sequences (shop_id, year, value) VALUES ({shopId}, {year}, 1)
            ON CONFLICT (shop_id, year) DO UPDATE SET value = work_order_sequences.value + 1
            RETURNING value AS "Value"
            """).ToListAsync(ct);
        return values[0];
    }

    public void Add(WorkOrder order) => db.WorkOrders.Add(order);
}

internal sealed class CustomerRepository(TamirhaneDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
}

internal sealed class DeviceRepository(TamirhaneDbContext db) : IDeviceRepository
{
    public Task<Device?> GetAsync(Guid id, CancellationToken ct) =>
        db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<Device>> ListByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Devices.Where(d => d.CustomerId == customerId).OrderBy(d => d.Brand).ThenBy(d => d.Model).ToListAsync(ct);

    public void Add(Device device) => db.Devices.Add(device);
}

internal sealed class PartRepository(TamirhaneDbContext db) : IPartRepository
{
    public Task<Part?> GetAsync(Guid id, CancellationToken ct) =>
        db.Parts.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, Guid? exceptPartId, CancellationToken ct) =>
        db.Parts.AnyAsync(p => p.Sku == sku && p.Id != exceptPartId, ct);

    public async Task<IReadOnlyList<PartStock>> ListActiveWithStockAsync(Guid branchId, CancellationToken ct)
    {
        var rows = await db.Parts
            .Where(p => p.IsActive)
            .Select(p => new
            {
                Part = p,
                Quantity = db.StockLevels.Where(l => l.PartId == p.Id && l.BranchId == branchId).Select(l => (int?)l.Quantity).FirstOrDefault() ?? 0,
            })
            .ToListAsync(ct);
        return rows.Select(r => new PartStock(r.Part, r.Quantity)).ToList();
    }

    public void Add(Part part) => db.Parts.Add(part);
}

internal sealed class StockRepository(TamirhaneDbContext db) : IStockRepository
{
    public Task<StockLevel?> GetLevelAsync(Guid partId, Guid branchId, CancellationToken ct) =>
        db.StockLevels.FirstOrDefaultAsync(l => l.PartId == partId && l.BranchId == branchId, ct);

    public async Task<BranchStock> GetBranchStockAsync(Guid branchId, IEnumerable<Guid> partIds, CancellationToken ct)
    {
        var ids = partIds.ToList();
        var levels = ids.Count == 0
            ? []
            : await db.StockLevels.Where(l => l.BranchId == branchId && ids.Contains(l.PartId)).ToListAsync(ct);
        return new BranchStock(branchId, levels);
    }

    public void AddLevel(StockLevel level) => db.StockLevels.Add(level);

    public void AddMovements(IEnumerable<StockMovement> movements) => db.StockMovements.AddRange(movements);
}
