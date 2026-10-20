using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Inventory;

namespace Tamirhane.Application.Inventory;

public sealed record PartCommand(string Sku, string Name, decimal CostPrice, decimal SalePrice, int CriticalLevel);

/// <summary>Yeni parça; isteğe bağlı açılış stoğu varsayılan şubeye "alış" olarak girer.</summary>
public sealed class AddPart(IPartRepository parts, IStockRepository stock, IShopSettings settings, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<Guid>> HandleAsync(PartCommand c, int openingQuantity, CancellationToken ct)
    {
        if (openingQuantity < 0)
            return InventoryErrors.InvalidQuantity;

        var part = Part.Create(user.ShopId, c.Sku, c.Name, c.CostPrice, c.SalePrice, c.CriticalLevel);
        if (part.IsFailure)
            return part.Error!;
        if (await parts.SkuExistsAsync(part.Value.Sku, null, ct))
            return ApplicationErrors.DuplicateSku;

        var shop = await settings.GetAsync(user.ShopId, ct);
        var level = StockLevel.Create(part.Value.Id, shop.DefaultBranchId);
        if (openingQuantity > 0)
        {
            var movement = level.Receive(openingQuantity, user.StaffId, TurkeyTime.Now(clock), null);
            if (movement.IsFailure)
                return movement.Error!;
            stock.AddMovements([movement.Value]);
        }

        parts.Add(part.Value);
        stock.AddLevel(level);
        await unitOfWork.SaveChangesAsync(ct);
        return part.Value.Id;
    }
}

public sealed class UpdatePart(IPartRepository parts, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(Guid partId, PartCommand c, CancellationToken ct)
    {
        var part = await parts.GetAsync(partId, ct);
        if (part is null)
            return ApplicationErrors.PartNotFound;

        var result = part.Update(c.Sku, c.Name, c.CostPrice, c.SalePrice, c.CriticalLevel);
        if (result.IsFailure)
            return result;
        if (await parts.SkuExistsAsync(part.Sku, part.Id, ct))
            return ApplicationErrors.DuplicateSku;

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Stok girişi ve sayım düzeltmesi; ikisi de varsayılan şubenin stok kaydına işlenir.</summary>
public sealed class ChangeStock(IPartRepository parts, IStockRepository stock, IShopSettings settings, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public Task<Result> ReceiveAsync(Guid partId, int quantity, string? note, CancellationToken ct) =>
        ApplyAsync(partId, (level, now) => level.Receive(quantity, user.StaffId, now, note), ct);

    public Task<Result> AdjustAsync(Guid partId, int countedQuantity, string reason, CancellationToken ct) =>
        ApplyAsync(partId, (level, now) => level.Adjust(countedQuantity, user.StaffId, now, reason), ct);

    private async Task<Result> ApplyAsync(Guid partId, Func<StockLevel, DateTimeOffset, Result<StockMovement>> change, CancellationToken ct)
    {
        var part = await parts.GetAsync(partId, ct);
        if (part is null)
            return ApplicationErrors.PartNotFound;

        var shop = await settings.GetAsync(user.ShopId, ct);
        var level = await stock.GetLevelAsync(part.Id, shop.DefaultBranchId, ct);
        if (level is null)
        {
            level = StockLevel.Create(part.Id, shop.DefaultBranchId);
            stock.AddLevel(level);
        }

        var movement = change(level, TurkeyTime.Now(clock));
        if (movement.IsFailure)
            return movement.Error!;

        stock.AddMovements([movement.Value]);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record LowStockItem(Guid PartId, string Sku, string Name, int Quantity, int CriticalLevel);

/// <summary>Kritik seviyeye ya da altına inmiş parçalar, en az kalandan başlayarak.</summary>
public sealed class ListLowStock(IPartRepository parts, IShopSettings settings, ICurrentUser user)
{
    public async Task<IReadOnlyList<LowStockItem>> HandleAsync(CancellationToken ct)
    {
        var shop = await settings.GetAsync(user.ShopId, ct);
        var all = await parts.ListActiveWithStockAsync(shop.DefaultBranchId, ct);
        return all
            .Where(p => p.Part.IsLow(p.Quantity))
            .OrderBy(p => p.Quantity)
            .ThenBy(p => p.Part.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), ignoreCase: true))
            .Select(p => new LowStockItem(p.Part.Id, p.Part.Sku, p.Part.Name, p.Quantity, p.Part.CriticalLevel))
            .ToList();
    }
}
