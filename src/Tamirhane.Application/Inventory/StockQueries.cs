using System.Globalization;
using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Inventory;

namespace Tamirhane.Application.Inventory;

/// <summary>Stok listesindeki bir parça, personelin şubesindeki adediyle. Alış fiyatı sadece sahibe (teknisyende boş).</summary>
public sealed record PartRow(
    Guid Id, string Sku, string Name, decimal SalePrice, decimal? CostPrice, int Quantity, int CriticalLevel, bool IsLow, bool IsActive);

public sealed record PartList(IReadOnlyList<PartRow> Parts, int ActiveCount, int LowCount);

/// <summary>Stok sayfası: arama (kod ya da ad, Türkçe harf duyarsız), sadece kritik stok, kullanımdan kalkanlar.</summary>
public sealed class ListParts(IPartRepository parts, ICurrentUser user)
{
    private static readonly CompareInfo Turkish = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public async Task<PartList> HandleAsync(string? query, bool lowOnly, bool includeInactive, CancellationToken ct)
    {
        var all = await parts.ListWithStockAsync(user.BranchId, includeInactive: true, ct);
        var active = all.Where(p => p.Part.IsActive).ToList();
        query = query?.Trim();

        var rows = all
            .Where(p => includeInactive || p.Part.IsActive)
            .Where(p => !lowOnly || (p.Part.IsActive && p.Part.IsLow(p.Quantity)))
            .Where(p => string.IsNullOrEmpty(query) || Contains(p.Part.Sku, query) || Contains(p.Part.Name, query))
            // Kritik stoktakiler önce, sonra ada göre.
            .OrderByDescending(p => p.Part.IsActive && p.Part.IsLow(p.Quantity))
            .ThenBy(p => p.Part.Name, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true))
            .Select(p => Row(p, user.IsOwner))
            .ToList();
        return new PartList(rows, active.Count, active.Count(p => p.Part.IsLow(p.Quantity)));
    }

    // Türkçede "i"nin büyüğü "İ": sadece Türkçe kurala bakılsa "ip13" araması "IP13-SCR" kodunu bulamazdı.
    // Kodlar İngilizce harfli, adlar Türkçe; iki kuraldan biri tutarsa eşleşir.
    private static bool Contains(string text, string query) =>
        Turkish.IndexOf(text, query, CompareOptions.IgnoreCase) >= 0
        || CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, query, CompareOptions.IgnoreCase) >= 0;

    internal static PartRow Row(PartStock p, bool isOwner) => new(
        p.Part.Id, p.Part.Sku, p.Part.Name, p.Part.SalePrice, isOwner ? p.Part.CostPrice : null,
        p.Quantity, p.Part.CriticalLevel, p.Part.IsActive && p.Part.IsLow(p.Quantity), p.Part.IsActive);
}

/// <summary>Stok hareketi: ne kadar, neden, kim, hangi iş emri için.</summary>
public sealed record MovementView(
    DateTimeOffset At, int Change, StockMovementReason Reason, string By, string? Note, Guid? WorkOrderId, string? WorkOrderNumber);

public sealed record PartDetail(PartRow Part, IReadOnlyList<MovementView> Movements);

/// <summary>Parça sayfası: bilgiler ve personelin şubesindeki son hareketler (en yenisi başta).</summary>
public sealed class GetPart(IPartRepository parts, IStockRepository stock, IStaffRepository staff, IWorkOrderRepository orders, ICurrentUser user)
{
    public const int MovementLimit = 100;

    public async Task<Result<PartDetail>> HandleAsync(Guid id, CancellationToken ct)
    {
        var part = await parts.GetAsync(id, ct);
        if (part is null)
            return ApplicationErrors.PartNotFound;

        var level = await stock.GetLevelAsync(part.Id, user.BranchId, ct);
        var movements = await stock.ListMovementsAsync(part.Id, user.BranchId, MovementLimit, ct);
        var names = await staff.NamesAsync(movements.Select(m => m.StaffId), ct);
        var numbers = await orders.NumbersAsync(movements.Select(m => m.WorkOrderId).OfType<Guid>(), ct);

        return new PartDetail(
            ListParts.Row(new PartStock(part, level?.Quantity ?? 0), user.IsOwner),
            movements.Select(m => new MovementView(m.At, m.Change, m.Reason, names.GetValueOrDefault(m.StaffId, "?"), m.Note,
                m.WorkOrderId, m.WorkOrderId is { } w ? numbers.GetValueOrDefault(w) : null)).ToList());
    }
}

/// <summary>Parçayı kullanımdan kaldırır ya da geri alır; geçmiş kayıtlar bozulmasın diye parça silinmez. Sadece sahip.</summary>
public sealed class SetPartActive(IPartRepository parts, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<Result> HandleAsync(Guid partId, bool active, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;
        var part = await parts.GetAsync(partId, ct);
        if (part is null)
            return ApplicationErrors.PartNotFound;

        if (active)
            part.Activate();
        else
            part.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
