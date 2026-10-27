using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Application.Dashboard;

/// <summary>Ciro sadece sahibe gösterilir; teknisyen için hiç hesaplanmaz (null).</summary>
public sealed record Revenue(decimal ThisMonth, decimal LastMonth);

public sealed record DashboardView(
    DateTimeOffset Now, WorkOrderCounts Counts, IReadOnlyList<WorkOrderRow> Oldest, Revenue? Revenue,
    IReadOnlyList<LowStockItem> LowStock)
{
    public bool HasOpenWork => Counts.Open > 0;
}

/// <summary>
/// Panel (#33): günün sayıları (tıklanınca iş emri listesi o süzgeçle açılır), en uzun bekleyen açık işler,
/// kritik stok ve sahibe aylık ciro. Ay, Türkiye saatine göre ayın ilk gününden başlar.
/// </summary>
public sealed class GetDashboard(
    IDashboardQueries queries, IWorkOrderRepository orders, ListLowStock lowStock, ICurrentUser user, TimeProvider clock)
{
    public const int OldestCount = 8;
    public const int LowStockCount = 5;

    public async Task<DashboardView> HandleAsync(CancellationToken ct)
    {
        var now = TurkeyTime.Now(clock);
        var counts = await queries.CountsAsync(now.AddDays(-ListWorkOrders.OverdueDays), ct);
        var oldest = counts.Open > 0
            ? await orders.SearchAsync(null, ListWorkOrders.OpenStatuses, null, OldestCount, ct)
            : [];

        Revenue? revenue = null;
        if (user.IsOwner)
        {
            var thisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
            var lastMonth = thisMonth.AddMonths(-1);
            revenue = new Revenue(
                await queries.RevenueAsync(thisMonth, thisMonth.AddMonths(1), ct),
                await queries.RevenueAsync(lastMonth, thisMonth, ct));
        }

        var low = (await lowStock.HandleAsync(ct)).Take(LowStockCount).ToList();
        return new DashboardView(now, counts, oldest, revenue, low);
    }
}
