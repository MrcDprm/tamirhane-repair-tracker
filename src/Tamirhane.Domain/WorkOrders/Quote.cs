namespace Tamirhane.Domain.WorkOrders;

/// <summary>Teklifin toplamları; kalemlerin yuvarlanmış tutarlarının toplamıdır.</summary>
public sealed record QuoteTotals(decimal Gross, decimal Discount, decimal Net, decimal Vat, decimal Total)
{
    public static QuoteTotals Of(IEnumerable<QuoteLine> lines)
    {
        decimal gross = 0, discount = 0, net = 0, vat = 0, total = 0;
        foreach (var line in lines)
        {
            gross += line.Gross;
            discount += line.Discount;
            net += line.Net;
            vat += line.Vat;
            total += line.Total;
        }
        return new QuoteTotals(gross, discount, net, vat, total);
    }
}

public enum QuoteDecisionKind
{
    Approved,
    Declined,
}

/// <summary>
/// Müşterinin teklif kararı. Müşteri takip sayfasından verdiyse StaffId boştur;
/// telefonla bildirdi ve personel onun adına kaydettiyse personelin kimliği tutulur.
/// </summary>
public sealed record QuoteDecision(QuoteDecisionKind Kind, string ByName, DateTimeOffset At, Guid? StaffId);
