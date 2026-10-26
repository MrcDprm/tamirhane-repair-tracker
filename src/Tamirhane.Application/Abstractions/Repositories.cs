using Tamirhane.Domain.Customers;
using Tamirhane.Domain.Inventory;
using Tamirhane.Domain.Shops;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Abstractions;

// Depolar sadece mevcut atölyenin kayıtlarını görür (kiracı filtresi Infrastructure'da).
// Başka atölyenin kaydı istenirse "yok" döner; varlığı bile sızmaz.

/// <summary>Takip sayfasının ihtiyacı olan atölye, şube, müşteri adı ve cihaz bilgisi (iş emri takip koduyla bulunduktan sonra).</summary>
public sealed record TrackingParties(
    string ShopName, string BranchName, string? BranchAddress, string? BranchPhone,
    string CustomerName, DeviceKind DeviceKind, string Brand, string Model);

/// <summary>
/// Oturumsuz okuma: müşteri giriş yapmadığı için kiracı filtresi kullanılamaz. Bütün kimlikler aynı atölyeye ait
/// olmalı; biri tutmazsa sonuç boştur. Sadece takip koduyla bulunmuş bir iş emrinin kimlikleriyle çağrılır.
/// </summary>
public interface ITrackingDirectory
{
    Task<TrackingParties?> GetAsync(Guid shopId, Guid branchId, Guid customerId, Guid deviceId, CancellationToken ct);
}

public interface IShopRepository
{
    Task<Shop?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Yeni atölye, ilk şubesi ve sahibi; kayıt oturum açılmadan yapılır.</summary>
    void Add(Shop shop, Branch firstBranch, Staff owner);
}

public interface IBranchRepository
{
    Task<Branch?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Atölyenin şubeleri, açılış sırasıyla (ilk şube başta).</summary>
    Task<IReadOnlyList<Branch>> ListAsync(CancellationToken ct);

    void Add(Branch branch);
}

public interface IStaffRepository
{
    /// <summary>Çıkarılmış personel de döner; çağıran <see cref="Staff.IsActive"/>'e bakar.</summary>
    Task<Staff?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Atölyede çalışan (çıkarılmamış) personel; önce sahip, sonra ada göre.</summary>
    Task<IReadOnlyList<Staff>> ListActiveAsync(CancellationToken ct);

    /// <summary>Verilen personelin görünen adları; çıkarılmış personel de dahil (iş emri geçmişi için).</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct);

    void Add(Staff staff);
}

/// <summary>
/// Davet ve davet eden atölyenin adı, dili ve ilk şubesi (yeni teknisyen oraya bağlanır);
/// davet sayfası oturum olmadan açılır.
/// </summary>
public sealed record InvitationWithShop(Invitation Invitation, string ShopName, string ShopLocale, Guid FirstBranchId);

public interface IInvitationRepository
{
    Task<Invitation?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Linkteki token'ın özetiyle; kiracı filtresi olmadan (davet edilen kişinin oturumu yok).
    /// Token 256 bit rastgele olduğu için tahminle başka atölyenin davetine ulaşılamaz.
    /// </summary>
    Task<InvitationWithShop?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Atölyenin bekleyen davetleri; <paramref name="email"/> verilirse sadece o adresinkiler.</summary>
    Task<IReadOnlyList<Invitation>> ListPendingAsync(DateTimeOffset now, string? email, CancellationToken ct);

    void Add(Invitation invitation);
}

public interface IWorkOrderRepository
{
    /// <summary>Kalemleri, ödemeleri ve geçmişiyle birlikte.</summary>
    Task<WorkOrder?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Takip sayfası için; kiracı filtresi olmadan, sadece koda göre.</summary>
    Task<WorkOrder?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct);

    /// <summary>
    /// Liste ve arama. Ölçüt verilirse türlerinden herhangi biri tutan iş emirleri; durumlar ve tarih süzgeçtir.
    /// Sıralama çağıranın işi; en fazla <paramref name="take"/> satır.
    /// </summary>
    Task<IReadOnlyList<Tamirhane.Application.WorkOrders.WorkOrderRow>> SearchAsync(
        Tamirhane.Application.WorkOrders.WorkOrderCriteria? criteria, IReadOnlyCollection<WorkOrderStatus>? statuses,
        DateTimeOffset? createdBefore, int take, CancellationToken ct);

    /// <summary>İş emirlerinin numaraları (stok hareketlerinde "hangi iş için" göstermek üzere).</summary>
    Task<IReadOnlyDictionary<Guid, string>> NumbersAsync(IEnumerable<Guid> ids, CancellationToken ct);

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

/// <summary>Bir parça ve bir şubedeki miktarı (stok kaydı yoksa 0).</summary>
public sealed record PartStock(Part Part, int Quantity);

public interface IPartRepository
{
    Task<Part?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, Guid? exceptPartId, CancellationToken ct);

    /// <summary>Atölyenin kullanımdaki parçaları ve verilen şubedeki miktarları.</summary>
    Task<IReadOnlyList<PartStock>> ListActiveWithStockAsync(Guid branchId, CancellationToken ct);

    /// <summary>Stok sayfası için; kullanımdan kaldırılanlar da istenebilir.</summary>
    Task<IReadOnlyList<PartStock>> ListWithStockAsync(Guid branchId, bool includeInactive, CancellationToken ct);

    void Add(Part part);
}

public interface IStockRepository
{
    Task<StockLevel?> GetLevelAsync(Guid partId, Guid branchId, CancellationToken ct);

    /// <summary>Şubenin verilen parçalara ait stok kayıtları; iş emriyle parça kullanma ve iade için.</summary>
    Task<BranchStock> GetBranchStockAsync(Guid branchId, IEnumerable<Guid> partIds, CancellationToken ct);

    /// <summary>Parçanın şubedeki hareketleri, en yenisi başta.</summary>
    Task<IReadOnlyList<StockMovement>> ListMovementsAsync(Guid partId, Guid branchId, int take, CancellationToken ct);

    void AddLevel(StockLevel level);
    void AddMovements(IEnumerable<StockMovement> movements);
}
