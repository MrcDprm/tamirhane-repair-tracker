using Tamirhane.Application.Abstractions;
using Tamirhane.Domain.Common;

namespace Tamirhane.Application.Shops;

/// <summary>Atölyenin bütün verisini siler. Infrastructure toplu SQL ile uygular; Application sadece kuralları bilir.</summary>
public interface IShopEraser
{
    /// <summary>
    /// İş emirleri, müşteriler, cihazlar, stok, davetler, personel, şubeler, atölyenin kendisi ve personel hesapları;
    /// tek transaction'da, ya hepsi ya hiçbiri.
    /// </summary>
    Task DeleteAsync(Guid shopId, CancellationToken ct);
}

public static class DeleteShopErrors
{
    public static readonly Error WrongPassword = Error.Validation("shop.delete.wrong_password");

    /// <summary>Demo atölyenin şifresi yok; zaten 24 saat sonra kendiliğinden silinir.</summary>
    public static readonly Error Demo = Error.Validation("shop.delete.demo");
}

/// <summary>
/// Sahip atölyesini kalıcı olarak siler (KVKK: veri sahibinin silme hakkı, gizlilik metnindeki söz). Geri alınamaz
/// olduğu için sahibin şifresi yeniden istenir: açık kalmış bir oturumdan başkası atölyeyi silemesin.
/// Silinince personelin hesapları da gider; açık oturumları güvenlik damgası kontrolünde düşer.
/// </summary>
public sealed class DeleteShop(IShopRepository shops, IStaffRepository staff, IAccountService accounts, IShopEraser eraser, ICurrentUser user)
{
    public async Task<Result> HandleAsync(string password, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var shop = await shops.GetAsync(user.ShopId, ct) ?? throw new InvalidOperationException("Oturumun atölyesi yok.");
        if (shop.IsDemo)
            return DeleteShopErrors.Demo;

        var owner = await staff.GetAsync(user.StaffId, ct);
        if (owner?.UserId is not { } userId || !await accounts.CheckPasswordAsync(userId, password ?? "", ct))
            return DeleteShopErrors.WrongPassword;

        await eraser.DeleteAsync(shop.Id, ct);
        return Result.Success();
    }
}
