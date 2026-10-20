using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Abstractions;

// Depolar sadece mevcut atölyenin kayıtlarını görür (kiracı filtresi Infrastructure'da).
// Başka atölyenin kaydı istenirse "yok" döner; varlığı bile sızmaz.

public interface IWorkOrderRepository
{
    /// <summary>Kalemleri, ödemeleri ve geçmişiyle birlikte.</summary>
    Task<WorkOrder?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Takip sayfası için; kiracı filtresi olmadan, sadece koda göre.</summary>
    Task<WorkOrder?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct);

    /// <summary>Atölyenin o yılki sıradaki iş emri numarası (1'den başlar).</summary>
    Task<int> NextSequenceAsync(Guid shopId, int year, CancellationToken ct);

    void Add(WorkOrder order);
}

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(Guid id, CancellationToken ct);
    Task<Customer?> FindByPhoneAsync(PhoneNumber phone, CancellationToken ct);
    void Add(Customer customer);
}

public interface IDeviceRepository
{
    Task<Device?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Device>> ListByCustomerAsync(Guid customerId, CancellationToken ct);
    void Add(Device device);
}

public interface IPartRepository
{
    Task<Part?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, Guid? exceptPartId, CancellationToken ct);
    void Add(Part part);
}

public interface IStockRepository
{
    Task<StockLevel?> GetLevelAsync(Guid partId, Guid branchId, CancellationToken ct);

    /// <summary>Şubenin verilen parçalara ait stok kayıtları; iş emriyle parça kullanma ve iade için.</summary>
    Task<BranchStock> GetBranchStockAsync(Guid branchId, IEnumerable<Guid> partIds, CancellationToken ct);

    void AddLevel(StockLevel level);
    void AddMovements(IEnumerable<StockMovement> movements);
}
