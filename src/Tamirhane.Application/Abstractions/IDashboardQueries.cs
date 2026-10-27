namespace Tamirhane.Application.Abstractions;

/// <summary>Paneldeki sayılar. Teslime hazır: onarılmış ve onarım istenmemiş, teslim alınmayı bekleyen cihazlar.</summary>
public sealed record WorkOrderCounts(int Open, int AwaitingApproval, int InRepair, int ReadyForPickup, int Overdue);

/// <summary>Panel sorguları; oturumdaki atölyenin kayıtları üzerinde (kiracı filtresi).</summary>
public interface IDashboardQueries
{
    /// <summary>Açık işlerin durumlara göre sayısı; tek sorgu. <paramref name="overdueBefore"/> öncesinde açılmış açık işler gecikmiş sayılır.</summary>
    Task<WorkOrderCounts> CountsAsync(DateTimeOffset overdueBefore, CancellationToken ct);

    /// <summary>Aralıktaki tahsilat eksi iade (kasaya giren net para). Bitiş hariç.</summary>
    Task<decimal> RevenueAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
}
