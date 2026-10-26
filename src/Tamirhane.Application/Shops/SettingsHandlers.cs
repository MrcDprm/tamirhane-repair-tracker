using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Shops;

/// <summary>Ayarlar sayfasındaki atölye bilgileri.</summary>
public sealed record ShopSettingsCommand(string Name, int WarrantyDays, decimal DefaultVatRate, string Locale);

public sealed class GetShopSettings(IShopRepository shops, ICurrentUser user)
{
    public async Task<Result<ShopSettingsCommand>> HandleAsync(CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var shop = await shops.GetAsync(user.ShopId, ct) ?? throw new InvalidOperationException("Oturumun atölyesi yok.");
        return new ShopSettingsCommand(shop.Name, shop.WarrantyDays, shop.DefaultVatRate, shop.Locale);
    }
}

/// <summary>Atölye adı, garanti süresi, varsayılan KDV ve müşteri yazışmalarının dili; sadece sahip.</summary>
public sealed class UpdateShopSettings(IShopRepository shops, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<Result> HandleAsync(ShopSettingsCommand c, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var shop = await shops.GetAsync(user.ShopId, ct) ?? throw new InvalidOperationException("Oturumun atölyesi yok.");
        var updated = shop.UpdateSettings(c.Name, c.WarrantyDays, c.DefaultVatRate, c.Locale);
        if (updated.IsFailure)
            return updated;

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
