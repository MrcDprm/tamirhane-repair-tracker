namespace Tamirhane.Application.Abstractions;

/// <summary>Atölyenin iş kurallarını etkileyen ayarları.</summary>
public sealed record ShopSettings(int WarrantyDays, decimal DefaultVatRate);

public interface IShopSettings
{
    Task<ShopSettings> GetAsync(Guid shopId, CancellationToken ct);
}
