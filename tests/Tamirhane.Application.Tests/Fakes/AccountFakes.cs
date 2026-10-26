using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Fakes;

/// <summary>Bellek içi hesaplar; gönderilen e-postaları da kaydeder.</summary>
public sealed class FakeAccounts : IAccountService
{
    public Dictionary<string, Guid> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(Guid UserId, string Locale)> ConfirmationsSent { get; } = [];
    public List<(string Email, string Locale)> AlreadyRegisteredSent { get; } = [];
    public Error? NextPasswordError { get; set; }
    public HashSet<Guid> Confirmed { get; } = [];
    public List<Guid> Deleted { get; } = [];
    public List<StaffInvitationEmail> InvitationsSent { get; } = [];

    public Task<Result<Guid>> CreateAsync(string email, string password, CancellationToken ct)
    {
        if (Users.ContainsKey(email))
            return Task.FromResult<Result<Guid>>(AccountErrors.EmailTaken);
        if (NextPasswordError is { } error)
            return Task.FromResult<Result<Guid>>(error);

        var id = Guid.NewGuid();
        Users[email] = id;
        return Task.FromResult<Result<Guid>>(id);
    }

    public async Task<Result<Guid>> CreateConfirmedAsync(string email, string password, CancellationToken ct)
    {
        var created = await CreateAsync(email, password, ct);
        if (created.IsSuccess)
            Confirmed.Add(created.Value);
        return created;
    }

    public Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        Deleted.Add(userId);
        foreach (var email in Users.Where(u => u.Value == userId).Select(u => u.Key).ToList())
            Users.Remove(email);
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(StaffInvitationEmail invitation, CancellationToken ct)
    {
        InvitationsSent.Add(invitation);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(Guid userId, string locale, CancellationToken ct)
    {
        ConfirmationsSent.Add((userId, locale));
        return Task.CompletedTask;
    }

    public Task SendAlreadyRegisteredAsync(string email, string locale, CancellationToken ct)
    {
        AlreadyRegisteredSent.Add((email, locale));
        return Task.CompletedTask;
    }

    // Oturum işleri kullanım senaryolarında yok; gerçek Identity ile Web testlerinde denenir.
    public Task<SignInOutcome> SignInAsync(string email, string password, bool remember, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task SignOutAsync() => throw new NotSupportedException();

    public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task ResendEmailConfirmationAsync(string email, CancellationToken ct) => throw new NotSupportedException();

    public Task SendPasswordResetAsync(string email, CancellationToken ct) => throw new NotSupportedException();

    public Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken ct) =>
        throw new NotSupportedException();
}

public sealed class FakeShops : IShopRepository
{
    public List<(Shop Shop, Staff Owner)> Items { get; } = [];

    public Task<Shop?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Select(i => i.Shop).FirstOrDefault(s => s.Id == id));

    public void Add(Shop shop, Staff owner) => Items.Add((shop, owner));
}

public sealed class FakeStaff(ICurrentUser user) : IStaffRepository
{
    public List<Staff> Items { get; } = [];

    public Task<Staff?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(s => s.Id == id && s.ShopId == user.ShopId));

    public Task<IReadOnlyList<Staff>> ListActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Staff>>(Items.Where(s => s.ShopId == user.ShopId && s.IsActive)
            .OrderBy(s => s.Role).ThenBy(s => s.DisplayName).ToList());

    public void Add(Staff staff) => Items.Add(staff);
}

/// <summary>Atölyelerin adları yanında; token ile arama kiracı filtresi olmadan, gerçeği gibi.</summary>
public sealed class FakeInvitations(ICurrentUser user, FakeShops shops) : IInvitationRepository
{
    public List<Invitation> Items { get; } = [];

    public Task<Invitation?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id && i.ShopId == user.ShopId));

    public Task<InvitationWithShop?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct)
    {
        var invitation = Items.FirstOrDefault(i => i.TokenHash.SequenceEqual(tokenHash));
        var shop = shops.Items.Select(s => s.Shop).FirstOrDefault(s => s.Id == invitation?.ShopId);
        return Task.FromResult(invitation is null || shop is null ? null : new InvitationWithShop(invitation, shop.Name, shop.Locale));
    }

    public Task<IReadOnlyList<Invitation>> ListPendingAsync(DateTimeOffset now, string? email, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Invitation>>(Items
            .Where(i => i.ShopId == user.ShopId && i.IsPending(now) && (email is null || i.Email == email)).ToList());

    public void Add(Invitation invitation) => Items.Add(invitation);
}
