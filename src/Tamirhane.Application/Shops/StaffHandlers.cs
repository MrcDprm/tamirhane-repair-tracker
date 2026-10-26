using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Shops;

// Personel yönetimi sadece sahibin işi. Web katmanı sayfaları rol politikasıyla kapatır; senaryolar da
// kendileri kontrol eder, böylece bir controller'da politika unutulsa bile teknisyen personele dokunamaz.

/// <summary>
/// Sahip e-postayla teknisyen davet eder. Aynı adrese bekleyen eski davet geri alınır: yeni link gidince eskisi çalışmaz.
/// Bu adresin sistemde hesabı olup olmadığı burada söylenmez; davet kabulünde ortaya çıkar, onu da sadece adresin sahibi görür.
/// </summary>
public sealed class InviteStaff(
    IInvitationRepository invitations, IShopRepository shops, IStaffRepository staff, IAccountService accounts,
    IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    /// <summary>Atölyeyi başkalarına e-posta yağdırmanın aracı yapmamak için bekleyen davet sınırı.</summary>
    public const int MaxPendingInvitations = 20;

    public async Task<Result<Guid>> HandleAsync(string email, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var now = TurkeyTime.Now(clock);
        var created = Invitation.Create(user.ShopId, email, user.StaffId, now);
        if (created.IsFailure)
            return created.Error!;
        var (invitation, token) = created.Value;

        var pending = await invitations.ListPendingAsync(now, null, ct);
        var sameAddress = pending.Where(i => i.Email == invitation.Email).ToList();
        if (pending.Count - sameAddress.Count >= MaxPendingInvitations)
            return ApplicationErrors.TooManyInvitations;

        foreach (var old in sameAddress)
            old.Revoke(now);
        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(ct);

        var shop = await shops.GetAsync(user.ShopId, ct) ?? throw new InvalidOperationException("Oturumun atölyesi yok.");
        var inviter = await staff.GetAsync(user.StaffId, ct) ?? throw new InvalidOperationException("Oturumun personeli yok.");
        await accounts.SendInvitationAsync(
            new StaffInvitationEmail(invitation.Email, token, shop.Name, inviter.DisplayName, shop.Locale, invitation.ExpiresAt), ct);
        return invitation.Id;
    }
}

public sealed record InvitationView(string ShopName, string Email, DateTimeOffset ExpiresAt);

/// <summary>Davet linki açılınca: geçerliyse hangi atölyeye, hangi adresle davet edildiği.</summary>
public sealed class GetInvitation(IInvitationRepository invitations, TimeProvider clock)
{
    public async Task<Result<InvitationView>> HandleAsync(string? token, CancellationToken ct)
    {
        var found = await AcceptInvitation.FindPendingAsync(invitations, token, TurkeyTime.Now(clock), ct);
        return found is null
            ? ShopErrors.InvalidInvitation
            : new InvitationView(found.ShopName, found.Invitation.Email, found.Invitation.ExpiresAt);
    }
}

public sealed record AcceptInvitationCommand(string Token, string DisplayName, string Password);

/// <summary>
/// Davet edilen kişi adını ve şifresini seçer; hesabı ve teknisyen kaydı tek transaction'da açılır, davet kullanılmış olur.
/// Başarılı sonuç hesabın e-postasıdır (oturum açmak için).
/// </summary>
public sealed class AcceptInvitation(
    IInvitationRepository invitations, IStaffRepository staff, IAccountService accounts, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<string>> HandleAsync(AcceptInvitationCommand c, CancellationToken ct)
    {
        var now = TurkeyTime.Now(clock);
        var found = await FindPendingAsync(invitations, c.Token, now, ct);
        if (found is null)
            return ShopErrors.InvalidInvitation;
        var invitation = found.Invitation;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        // Kayıt formunun aksine "bu e-posta kayıtlı" burada söylenebilir: linki sadece adresin sahibi aldı.
        // Bir hesap tek atölyede çalışır; aynı adresle ikinci atölyeye katılmak için önce eski hesap silinmeli.
        var account = await accounts.CreateConfirmedAsync(invitation.Email, c.Password, ct);
        if (account.IsFailure)
            return account.Error!;

        var technician = Staff.Create(invitation.ShopId, account.Value, StaffRole.Technician, c.DisplayName, found.FirstBranchId);
        if (technician.IsFailure)
            return technician.Error!; // transaction commit edilmeden kapanır, hesap da geri alınır

        var accepted = invitation.Accept(now);
        if (accepted.IsFailure)
            return accepted.Error!;
        staff.Add(technician.Value);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return invitation.Email;
    }

    internal static async Task<InvitationWithShop?> FindPendingAsync(
        IInvitationRepository invitations, string? token, DateTimeOffset now, CancellationToken ct)
    {
        if (Invitation.HashToken(token) is not { } hash)
            return null;
        var found = await invitations.FindByTokenHashAsync(hash, ct);
        return found is not null && found.Invitation.IsPending(now) ? found : null;
    }
}

/// <summary>Bekleyen daveti geri alır (yanlış adres yazıldıysa).</summary>
public sealed class RevokeInvitation(IInvitationRepository invitations, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid invitationId, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var invitation = await invitations.GetAsync(invitationId, ct);
        if (invitation is null)
            return ApplicationErrors.InvitationNotFound;

        invitation.Revoke(TurkeyTime.Now(clock));
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Personeli çıkarır: kaydı iş emri geçmişi için kalır, hesabı silinir. Hesap silinince güvenlik damgası
/// doğrulanamaz, açık oturumu bir sonraki istekte düşer. Sahip (kendisi) çıkarılamaz.
/// </summary>
public sealed class RemoveStaff(IStaffRepository staff, IAccountService accounts, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result> HandleAsync(Guid staffId, CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var member = await staff.GetAsync(staffId, ct);
        if (member is null)
            return ShopErrors.StaffNotFound;

        var userId = member.UserId;
        var removed = member.Remove(TurkeyTime.Now(clock));
        if (removed.IsFailure)
            return removed;

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (userId is { } id)
            await accounts.DeleteAsync(id, ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }
}

public sealed record StaffMember(Guid Id, string DisplayName, StaffRole Role, bool IsYou);
public sealed record PendingInvitation(Guid Id, string Email, DateTimeOffset ExpiresAt);
public sealed record StaffOverview(IReadOnlyList<StaffMember> Members, IReadOnlyList<PendingInvitation> Invitations);

/// <summary>Personel sayfası: çalışanlar ve bekleyen davetler.</summary>
public sealed class ListStaff(IStaffRepository staff, IInvitationRepository invitations, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<StaffOverview>> HandleAsync(CancellationToken ct)
    {
        if (!user.IsOwner)
            return ApplicationErrors.OwnerOnly;

        var members = await staff.ListActiveAsync(ct);
        var pending = await invitations.ListPendingAsync(TurkeyTime.Now(clock), null, ct);
        return new StaffOverview(
            members.Select(s => new StaffMember(s.Id, s.DisplayName, s.Role, s.Id == user.StaffId)).ToList(),
            pending.OrderBy(i => i.Email).Select(i => new PendingInvitation(i.Id, i.Email, i.ExpiresAt)).ToList());
    }
}
