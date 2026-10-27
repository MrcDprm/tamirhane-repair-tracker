using System.Text.RegularExpressions;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Customers;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>İş emri listesinin süzgeçleri. "Açık" varsayılan: tezgâhta bekleyen her şey.</summary>
public enum WorkOrderFilter
{
    Open,
    AwaitingApproval,
    InRepair,
    Ready,
    /// <summary>30 günü geçmiş açık işler.</summary>
    Overdue,
    Closed,
    All,
}

/// <summary>Listede bir satır.</summary>
public sealed record WorkOrderRow(
    Guid Id, string Number, string Brand, string Model, string Complaint, string CustomerName,
    WorkOrderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? DeliveredAt);

/// <summary>
/// Aranan şeyin ne olduğu, yazılandan çıkarılır: iş emri numarası (2026-0042 ya da sadece 42), telefon, IMEI,
/// takip kodu ya da serbest metin (müşteri adı, cihaz marka ve modeli, seri no). Birden çoğu aynı anda olabilir.
/// </summary>
public sealed partial record WorkOrderCriteria(
    string? Text, string? Number, string? NumberSuffix, PhoneNumber? Phone, string? Imei, string? TrackingCode)
{
    public const int MaxQueryLength = 100;

    public static WorkOrderCriteria? Parse(string? query)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
            return null;
        if (query.Length > MaxQueryLength)
            query = query[..MaxQueryLength];

        string? number = null;
        if (FullNumber().Match(query) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var year)
            && int.TryParse(m.Groups[2].Value, out var sequence) && year >= 2000 && sequence > 0)
            number = WorkOrderNumber.Format(year, sequence);

        string? suffix = null, imei = null;
        PhoneNumber? phone = null;
        var text = query;
        if (number is null && NumericOnly().IsMatch(query))
        {
            var digits = new string(query.Where(char.IsAsciiDigit).ToArray());
            imei = Tamirhane.Domain.Customers.Imei.Normalize(query);
            phone = PhoneNumber.Parse(query) is { IsSuccess: true } p ? p.Value : null;
            if (digits.Length is > 0 and <= 6)
                suffix = digits.PadLeft(4, '0');
            text = null; // rakamlarla isim aranmaz
        }

        return new WorkOrderCriteria(number is null ? text : null, number, suffix, phone, imei, Tamirhane.Domain.WorkOrders.TrackingCode.Normalize(query));
    }

    [GeneratedRegex(@"^(\d{4})\s*-\s*(\d{1,6})$")]
    private static partial Regex FullNumber();

    [GeneratedRegex(@"^[\d\s\-+().]+$")]
    private static partial Regex NumericOnly();
}

public sealed record WorkOrderList(IReadOnlyList<WorkOrderRow> Rows, WorkOrderFilter Filter, string? Query, DateTimeOffset Now, bool Truncated);

/// <summary>
/// İş emri listesi ve hızlı arama (atölyenin bütün şubeleri). Açık işlerde en uzun bekleyen başta, kapalılarda en yeni başta.
/// </summary>
public sealed class ListWorkOrders(IWorkOrderRepository orders, TimeProvider clock)
{
    public const int Limit = 200;
    public const int OverdueDays = 30;

    /// <summary>Tezgâhta bekleyen işler: teslim edilmemiş ve iptal edilmemiş (panel de bunu kullanır).</summary>
    public static readonly WorkOrderStatus[] OpenStatuses =
    [
        WorkOrderStatus.Received, WorkOrderStatus.Diagnosing, WorkOrderStatus.AwaitingApproval,
        WorkOrderStatus.InRepair, WorkOrderStatus.Ready, WorkOrderStatus.Declined,
    ];

    public async Task<WorkOrderList> HandleAsync(string? query, WorkOrderFilter filter, CancellationToken ct)
    {
        var now = TurkeyTime.Now(clock);
        WorkOrderStatus[]? statuses = filter switch
        {
            WorkOrderFilter.Open or WorkOrderFilter.Overdue => OpenStatuses,
            WorkOrderFilter.AwaitingApproval => [WorkOrderStatus.AwaitingApproval],
            WorkOrderFilter.InRepair => [WorkOrderStatus.InRepair],
            WorkOrderFilter.Ready => [WorkOrderStatus.Ready, WorkOrderStatus.Declined],
            WorkOrderFilter.Closed => [WorkOrderStatus.Delivered, WorkOrderStatus.Cancelled],
            _ => null,
        };
        var createdBefore = filter == WorkOrderFilter.Overdue ? now.AddDays(-OverdueDays) : (DateTimeOffset?)null;
        var criteria = WorkOrderCriteria.Parse(query);

        var rows = await orders.SearchAsync(criteria, statuses, createdBefore, Limit + 1, ct);
        var openFirst = filter is WorkOrderFilter.Closed or WorkOrderFilter.All
            ? rows.OrderByDescending(r => r.CreatedAt)
            : rows.OrderBy(r => r.CreatedAt);
        return new WorkOrderList(openFirst.Take(Limit).ToList(), filter, query?.Trim(), now, rows.Count > Limit);
    }
}
