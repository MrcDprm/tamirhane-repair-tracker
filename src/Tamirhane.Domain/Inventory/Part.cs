using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Inventory;

/// <summary>Atölyenin stokta tuttuğu bir parça (ör. "iPhone 13 ekran paneli"). Miktar şube başına <see cref="StockLevel"/>'da.</summary>
public sealed class Part : Entity
{
    public const int MaxSkuLength = 40;
    public const int MaxNameLength = 200;
    public const decimal MaxPrice = 10_000_000m;

    private Part() { }

    public Guid ShopId { get; private set; }
    public string Sku { get; private set; } = "";
    public string Name { get; private set; } = "";
    public decimal CostPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    /// <summary>Miktar bu sayıya ya da altına inince uyarı verilir. 0 = uyarı yok.</summary>
    public int CriticalLevel { get; private set; }
    public bool IsActive { get; private set; }

    public static Result<Part> Create(Guid shopId, string sku, string name, decimal costPrice, decimal salePrice, int criticalLevel)
    {
        var part = new Part { ShopId = shopId, IsActive = true };
        var result = part.Update(sku, name, costPrice, salePrice, criticalLevel);
        return result.IsSuccess ? part : result.Error!;
    }

    public Result Update(string sku, string name, decimal costPrice, decimal salePrice, int criticalLevel)
    {
        sku = sku?.Trim().ToUpperInvariant() ?? "";
        name = name?.Trim() ?? "";
        if (sku.Length == 0 || sku.Length > MaxSkuLength)
            return InventoryErrors.InvalidSku;
        if (name.Length == 0 || name.Length > MaxNameLength)
            return InventoryErrors.InvalidName;
        if (!IsValidPrice(costPrice) || !IsValidPrice(salePrice))
            return InventoryErrors.InvalidPrice;
        if (criticalLevel < 0)
            return InventoryErrors.InvalidCriticalLevel;

        Sku = sku;
        Name = name;
        CostPrice = costPrice;
        SalePrice = salePrice;
        CriticalLevel = criticalLevel;
        return Result.Success();
    }

    /// <summary>Kullanılmayan parça listeden kalkar ama geçmiş kayıtları bozulmasın diye silinmez.</summary>
    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool IsLow(int quantity) => CriticalLevel > 0 && quantity <= CriticalLevel;

    private static bool IsValidPrice(decimal price) =>
        price >= 0 && price <= MaxPrice && decimal.Round(price, 2) == price;
}
