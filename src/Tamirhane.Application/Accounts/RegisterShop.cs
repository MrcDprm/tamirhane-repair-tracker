using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Accounts;

/// <summary>Atölye kaydı: atölye ve sahibinin hesabı tek adımda açılır.</summary>
public sealed record RegisterShopCommand(string ShopName, string OwnerName, string Email, string Password, string Locale);

public sealed class RegisterShop(IAccountService accounts, IShopRepository shops, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>
    /// Başarılı sonuç "doğrulama e-postası gönderildi" demektir; e-posta zaten kayıtlıysa da aynı sonuç döner
    /// (adresin sahibine ayrı bir e-posta gider), böylece form "bu e-posta kayıtlı mı" sorusuna cevap vermez.
    /// </summary>
    public async Task<Result> HandleAsync(RegisterShopCommand c, CancellationToken ct)
    {
        var shop = Shop.Create(c.ShopName, c.Locale, TurkeyTime.Now(clock));
        if (shop.IsFailure)
            return shop.Error!;

        Guid userId;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(ct))
        {
            // Hesabı Identity kendisi kaydeder; atölye ve sahibi aynı transaction'da yazılmazsa hesap da geri alınır.
            var account = await accounts.CreateAsync(c.Email, c.Password, ct);
            if (account.Error == AccountErrors.EmailTaken)
            {
                await accounts.SendAlreadyRegisteredAsync(c.Email, shop.Value.Locale, ct);
                return Result.Success();
            }
            if (account.IsFailure)
                return account.Error!;

            var owner = Staff.Create(shop.Value.Id, account.Value, StaffRole.Owner, c.OwnerName);
            if (owner.IsFailure)
                return owner.Error!;

            shops.Add(shop.Value, owner.Value);
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            userId = account.Value;
        }

        await accounts.SendEmailConfirmationAsync(userId, shop.Value.Locale, ct);
        return Result.Success();
    }
}
