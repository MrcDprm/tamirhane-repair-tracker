namespace Tamirhane.Application.Abstractions;

/// <summary>Atölyenin iş kurallarını etkileyen ayarları.</summary>
/// <summary>Locale: müşteriye giden yazıların dili ("tr" ya da "en").</summary>
public sealed record ShopSettings(int WarrantyDays, decimal DefaultVatRate, string Locale = "tr");

public interface IShopSettings
{
    Task<ShopSettings> GetAsync(Guid shopId, CancellationToken ct);
}
