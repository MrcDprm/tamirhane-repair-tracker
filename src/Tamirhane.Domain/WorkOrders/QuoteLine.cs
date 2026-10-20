using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.WorkOrders;

public enum QuoteLineKind
{
    Part,
    Labor,
}

/// <summary>
/// Teklifin bir kalemi. Tutarlar kalem başına hesaplanır ve her adımda 2 basamağa yuvarlanır:
/// brüt = miktar × birim fiyat → iskonto → net → KDV → toplam.
/// </summary>
public sealed class QuoteLine : Entity
{
    public const int MaxDescriptionLength = 200;
    public const decimal MaxQuantity = 10_000m;
    public const decimal MaxUnitPrice = 10_000_000m;

    private QuoteLine() { }

    public Guid WorkOrderId { get; private set; }
    public QuoteLineKind Kind { get; private set; }
    /// <summary>Stoktaki parça; stokta olmayan, sipariş edilen parçada boştur.</summary>
    public Guid? PartId { get; private set; }
    public string Description { get; private set; } = "";
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    /// <summary>Yüzde olarak, ör. 20.</summary>
    public decimal VatRate { get; private set; }
    /// <summary>Yüzde olarak, 0-100. Garanti kapsamındaki ücretsiz işçilikte 100.</summary>
    public decimal DiscountRate { get; private set; }

    public decimal Gross => Money(Quantity * UnitPrice);
    public decimal Discount => Money(Gross * DiscountRate / 100m);
    public decimal Net => Gross - Discount;
    public decimal Vat => Money(Net * VatRate / 100m);
    public decimal Total => Net + Vat;

    internal static Result<QuoteLine> Create(
        Guid workOrderId, QuoteLineKind kind, Guid? partId, string description,
        decimal quantity, decimal unitPrice, decimal vatRate, decimal discountRate)
    {
        description = description?.Trim() ?? "";
        if (description.Length == 0 || description.Length > MaxDescriptionLength)
            return QuoteErrors.InvalidDescription;
        if (quantity <= 0 || quantity > MaxQuantity || decimal.Round(quantity, 2) != quantity)
            return QuoteErrors.InvalidQuantity;
        if (unitPrice < 0 || unitPrice > MaxUnitPrice || decimal.Round(unitPrice, 2) != unitPrice)
            return QuoteErrors.InvalidUnitPrice;
        if (vatRate < 0 || vatRate > 100)
            return QuoteErrors.InvalidVatRate;
        if (discountRate < 0 || discountRate > 100)
            return QuoteErrors.InvalidDiscount;

        return new QuoteLine
        {
            WorkOrderId = workOrderId,
            Kind = kind,
            PartId = kind == QuoteLineKind.Part ? partId : null,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            DiscountRate = discountRate,
        };
    }

    internal static decimal Money(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
