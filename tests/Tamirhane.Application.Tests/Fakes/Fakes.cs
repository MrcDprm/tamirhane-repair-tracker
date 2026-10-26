using Tamirhane.Application.Abstractions;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.Fakes;

// Bellek içi sahte uygulamalar: kullanım senaryoları veritabanı olmadan test edilir.
// Depolar kiracı filtresini taklit eder: başka atölyenin kaydı "yok" gibi davranır.

public sealed class FakeUser(Guid shopId, bool isOwner = true) : ICurrentUser
{
    public Guid ShopId { get; } = shopId;
    public Guid StaffId { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; } = Guid.NewGuid();
    public bool IsOwner { get; set; } = isOwner;
}

public sealed class FakeSettings(ShopSettings settings) : IShopSettings
{
    public Task<ShopSettings> GetAsync(Guid shopId, CancellationToken ct) => Task.FromResult(settings);
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }
    public int Commits { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task<ITransaction> BeginTransactionAsync(CancellationToken ct) =>
        Task.FromResult<ITransaction>(new FakeTransaction(this));

    private sealed class FakeTransaction(FakeUnitOfWork owner) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct)
        {
            owner.Commits++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeQueue : INotificationQueue
{
    public List<WorkOrderNotification> Sent { get; } = [];

    public void Enqueue(WorkOrderNotification notification) => Sent.Add(notification);
}

public sealed class FakeWorkOrders(ICurrentUser user) : IWorkOrderRepository
{
    public List<WorkOrder> Items { get; } = [];

    public Task<WorkOrder?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Find(o => o.Id == id && o.ShopId == user.ShopId));

    // Arama veritabanının işi (ILIKE, birleştirmeler); gerçek PostgreSQL ile Web testlerinde denenir.
    public Task<IReadOnlyList<WorkOrderRow>> SearchAsync(
        WorkOrderCriteria? criteria, IReadOnlyCollection<WorkOrderStatus>? statuses, DateTimeOffset? createdBefore, int take, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<Guid, string>> NumbersAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Items
            .Where(o => o.ShopId == user.ShopId && ids.Contains(o.Id)).ToDictionary(o => o.Id, o => o.Number));

    public Task<WorkOrder?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct) =>
        Task.FromResult(Items.Find(o => o.TrackingCode == trackingCode));

    public Task<int> NextSequenceAsync(Guid shopId, int year, CancellationToken ct) =>
        Task.FromResult(Items.Count(o => o.ShopId == shopId && o.Number.StartsWith($"{year}-")) + 1);

    public void Add(WorkOrder order) => Items.Add(order);
}

public sealed class FakeCustomers(ICurrentUser user) : ICustomerRepository
{
    public List<Customer> Items { get; } = [];

    public Task<Customer?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Find(c => c.Id == id && c.ShopId == user.ShopId));

    public Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken ct) =>
        Task.FromResult(Items.Find(c => c.Phone == phone && c.ShopId == user.ShopId));

    public void Add(Customer customer) => Items.Add(customer);
}

public sealed class FakeDevices(ICurrentUser user) : IDeviceRepository
{
    public List<Device> Items { get; } = [];

    public Task<Device?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Find(d => d.Id == id && d.ShopId == user.ShopId));

    public Task<IReadOnlyList<Device>> ListByCustomerAsync(Guid customerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Device>>(Items.Where(d => d.CustomerId == customerId && d.ShopId == user.ShopId).ToList());

    public void Add(Device device) => Items.Add(device);
}

public sealed class FakeParts(ICurrentUser user) : IPartRepository
{
    public List<Part> Items { get; } = [];

    public Task<Part?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Find(p => p.Id == id && p.ShopId == user.ShopId));

    public Task<bool> SkuExistsAsync(string sku, Guid? exceptPartId, CancellationToken ct) =>
        Task.FromResult(Items.Any(p => p.ShopId == user.ShopId && p.Sku == sku && p.Id != exceptPartId));

    public FakeStock? Stock { get; set; }

    public Task<IReadOnlyList<PartStock>> ListActiveWithStockAsync(Guid branchId, CancellationToken ct) =>
        ListWithStockAsync(branchId, includeInactive: false, ct);

    public Task<IReadOnlyList<PartStock>> ListWithStockAsync(Guid branchId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PartStock>>(Items
            .Where(p => p.ShopId == user.ShopId && (includeInactive || p.IsActive))
            .Select(p => new PartStock(p, Stock?.Levels.Find(l => l.PartId == p.Id && l.BranchId == branchId)?.Quantity ?? 0))
            .ToList());

    public void Add(Part part) => Items.Add(part);
}

public sealed class FakeStock : IStockRepository
{
    public List<StockLevel> Levels { get; } = [];
    public List<StockMovement> Movements { get; } = [];

    public Task<StockLevel?> GetLevelAsync(Guid partId, Guid branchId, CancellationToken ct) =>
        Task.FromResult(Levels.Find(l => l.PartId == partId && l.BranchId == branchId));

    public Task<BranchStock> GetBranchStockAsync(Guid branchId, IEnumerable<Guid> partIds, CancellationToken ct)
    {
        var ids = partIds.ToHashSet();
        return Task.FromResult(new BranchStock(branchId, Levels.Where(l => l.BranchId == branchId && ids.Contains(l.PartId))));
    }

    public Task<IReadOnlyList<StockMovement>> ListMovementsAsync(Guid partId, Guid branchId, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(Movements
            .Where(m => m.PartId == partId && m.BranchId == branchId).OrderByDescending(m => m.At).Take(take).ToList());

    public void AddLevel(StockLevel level) => Levels.Add(level);

    public void AddMovements(IEnumerable<StockMovement> movements) => Movements.AddRange(movements);
}
