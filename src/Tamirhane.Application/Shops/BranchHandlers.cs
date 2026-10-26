using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Shops;

public sealed record BranchItem(Guid Id, string Name, string? Address, bool IsCurrent);

/// <summary>Atölyenin şubeleri; personelin çalıştığı şube işaretli. Menüdeki şube seçici ve ayarlar sayfası için.</summary>
public sealed class ListBranches(IBranchRepository branches, ICurrentUser user)
{
    public async Task<IReadOnlyList<BranchItem>> HandleAsync(CancellationToken ct) =>
        (await branches.ListAsync(ct))
            .Select(b => new BranchItem(b.Id, b.Name, b.Address, b.Id == user.BranchId))
            .ToList();
}

public sealed record BranchCommand(string Name, string? Address);

/// <summary>Yeni şube; sadece sahip. Şubesi olmayan iş emri ve stok olmaz, bu yüzden şube silinmez.</summary>
public sealed class AddBranch(IBranchRepository branches, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<Guid>> HandleAsync(BranchCommand c, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;
        if ((await branches.ListAsync(ct)).Count >= Branch.MaxPerShop)
            return ShopErrors.TooManyBranches;

        var branch = Branch.Create(user.ShopId, c.Name, c.Address, TurkeyTime.Now(clock));
        if (branch.IsFailure)
            return branch.Error!;

        branches.Add(branch.Value);
        await unitOfWork.SaveChangesAsync(ct);
        return branch.Value.Id;
    }
}

public sealed class UpdateBranch(IBranchRepository branches, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<Result> HandleAsync(Guid branchId, BranchCommand c, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var branch = await branches.GetAsync(branchId, ct);
        if (branch is null)
            return ShopErrors.BranchNotFound;

        var updated = branch.Update(c.Name, c.Address);
        if (updated.IsFailure)
            return updated;

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Personel çalıştığı şubeyi değiştirir (teknisyen de: günün birinde öbür dükkâna geçen usta).
/// Oturumdaki şube bir sonraki istekte personel kaydından yeniden okunur.
/// </summary>
public sealed class SwitchBranch(IBranchRepository branches, IStaffRepository staff, IUnitOfWork unitOfWork, ICurrentUser user)
{
    public async Task<Result> HandleAsync(Guid branchId, CancellationToken ct)
    {
        var branch = await branches.GetAsync(branchId, ct);
        if (branch is null)
            return ShopErrors.BranchNotFound;
        var member = await staff.GetAsync(user.StaffId, ct) ?? throw new InvalidOperationException("Oturumun personeli yok.");

        var moved = member.MoveTo(branch);
        if (moved.IsFailure)
            return moved;

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
