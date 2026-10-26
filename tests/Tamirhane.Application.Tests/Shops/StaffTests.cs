using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Shops;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Shops;

public class StaffTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeAccounts _accounts = new();
    private readonly FakeShops _shops = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUser _user;
    private readonly FakeStaff _staff;
    private readonly FakeInvitations _invitations;
    private readonly Shop _shop;
    private readonly Staff _owner;

    public StaffTests()
    {
        _shop = Shop.Create("Kaya Telefon", "tr", _clock.GetLocalNow()).Value;
        _owner = Staff.Create(_shop.Id, Guid.NewGuid(), StaffRole.Owner, "Ayşe Kaya").Value;
        _shops.Add(_shop, _owner);
        _user = new FakeUser(_shop.Id) { StaffId = _owner.Id };
        _staff = new FakeStaff(_user);
        _staff.Add(_owner);
        _invitations = new FakeInvitations(_user, _shops);
    }

    private InviteStaff Invite() => new(_invitations, _shops, _staff, _accounts, _unitOfWork, _user, _clock);
    private AcceptInvitation Accept() => new(_invitations, _staff, _accounts, _unitOfWork, _clock);
    private GetInvitation Get() => new(_invitations, _clock);
    private RemoveStaff Remove() => new(_staff, _accounts, _unitOfWork, _user, _clock);
    private RevokeInvitation Revoke() => new(_invitations, _unitOfWork, _user, _clock);
    private ListStaff List() => new(_staff, _invitations, _user, _clock);

    private async Task<string> InviteAndGetTokenAsync(string email = "mehmet@example.com")
    {
        Assert.True((await Invite().HandleAsync(email, default)).IsSuccess);
        return _accounts.InvitationsSent[^1].Token;
    }

    private static AcceptInvitationCommand AcceptCommand(string token, string name = "Mehmet Demir") =>
        new(token, name, "mavi-tornavida-17");

    [Fact]
    public async Task Owner_invites_a_technician_by_email()
    {
        var result = await Invite().HandleAsync(" Mehmet@Example.com ", default);

        Assert.True(result.IsSuccess);
        var invitation = Assert.Single(_invitations.Items);
        Assert.Equal("mehmet@example.com", invitation.Email);
        Assert.Equal(_owner.Id, invitation.InvitedByStaffId);
        var email = Assert.Single(_accounts.InvitationsSent);
        Assert.Equal(("mehmet@example.com", "Kaya Telefon", "Ayşe Kaya", "tr"), (email.Email, email.ShopName, email.InvitedBy, email.Locale));
        Assert.Equal(invitation.TokenHash, Invitation.HashToken(email.Token));
        Assert.Equal(invitation.ExpiresAt, email.ExpiresAt);
    }

    [Fact]
    public async Task Technician_cannot_invite()
    {
        _user.IsOwner = false;

        Assert.Equal(ApplicationErrors.OwnerOnly, (await Invite().HandleAsync("mehmet@example.com", default)).Error);
        Assert.Empty(_invitations.Items);
        Assert.Empty(_accounts.InvitationsSent);
    }

    [Fact]
    public async Task Invalid_email_is_rejected_without_sending()
    {
        Assert.Equal(ShopErrors.InvalidEmail, (await Invite().HandleAsync("mehmet", default)).Error);
        Assert.Empty(_accounts.InvitationsSent);
    }

    [Fact]
    public async Task New_invitation_to_the_same_address_revokes_the_old_link()
    {
        var oldToken = await InviteAndGetTokenAsync();
        var newToken = await InviteAndGetTokenAsync("MEHMET@example.com");

        Assert.Equal(ShopErrors.InvalidInvitation, (await Get().HandleAsync(oldToken, default)).Error);
        Assert.True((await Get().HandleAsync(newToken, default)).IsSuccess);
    }

    [Fact]
    public async Task Pending_invitations_are_capped()
    {
        for (var i = 0; i < InviteStaff.MaxPendingInvitations; i++)
            await InviteAndGetTokenAsync($"kisi{i}@example.com");

        Assert.Equal(ApplicationErrors.TooManyInvitations, (await Invite().HandleAsync("fazla@example.com", default)).Error);
        // Bekleyen bir adrese yeniden davet sınırı aşmaz: eskisinin yerine geçer.
        Assert.True((await Invite().HandleAsync("kisi0@example.com", default)).IsSuccess);
    }

    [Fact]
    public async Task Invitation_page_shows_shop_and_address()
    {
        var token = await InviteAndGetTokenAsync();

        var view = (await Get().HandleAsync(token, default)).Value;

        Assert.Equal("Kaya Telefon", view.ShopName);
        Assert.Equal("mehmet@example.com", view.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("uydurma-token")]
    public async Task Unknown_or_malformed_link_is_invalid(string? token)
    {
        Assert.Equal(ShopErrors.InvalidInvitation, (await Get().HandleAsync(token, default)).Error);
    }

    [Fact]
    public async Task Accepting_creates_a_confirmed_account_and_a_technician()
    {
        var token = await InviteAndGetTokenAsync();

        var result = await Accept().HandleAsync(AcceptCommand(token), default);

        Assert.Equal("mehmet@example.com", result.Value);
        var userId = _accounts.Users["mehmet@example.com"];
        Assert.Contains(userId, _accounts.Confirmed);
        var technician = Assert.Single(_staff.Items, s => s.UserId == userId);
        Assert.Equal((_shop.Id, StaffRole.Technician, "Mehmet Demir"), (technician.ShopId, technician.Role, technician.DisplayName));
        Assert.NotNull(Assert.Single(_invitations.Items).AcceptedAt);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Invitation_link_works_only_once()
    {
        var token = await InviteAndGetTokenAsync();
        await Accept().HandleAsync(AcceptCommand(token), default);

        Assert.Equal(ShopErrors.InvalidInvitation, (await Accept().HandleAsync(AcceptCommand(token, "Başka Biri"), default)).Error);
        Assert.Equal(2, _staff.Items.Count);
    }

    [Fact]
    public async Task Expired_invitation_cannot_be_accepted()
    {
        var token = await InviteAndGetTokenAsync();
        _clock.Advance(Invitation.Lifetime);

        Assert.Equal(ShopErrors.InvalidInvitation, (await Accept().HandleAsync(AcceptCommand(token), default)).Error);
        Assert.Empty(_accounts.Users);
    }

    [Fact]
    public async Task Address_with_an_existing_account_cannot_join_a_second_shop()
    {
        var token = await InviteAndGetTokenAsync();
        _accounts.Users["mehmet@example.com"] = Guid.NewGuid();

        Assert.Equal(AccountErrors.EmailTaken, (await Accept().HandleAsync(AcceptCommand(token), default)).Error);
        Assert.Null(Assert.Single(_invitations.Items).AcceptedAt); // davet boşa harcanmaz
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Weak_password_or_bad_name_keeps_the_invitation_usable()
    {
        var token = await InviteAndGetTokenAsync();

        Assert.Equal(ShopErrors.InvalidDisplayName, (await Accept().HandleAsync(AcceptCommand(token, " "), default)).Error);
        _accounts.Users.Clear(); // gerçek veritabanında commit edilmeyen transaction hesabı geri alır
        _accounts.NextPasswordError = AccountErrors.PasswordTooShort;
        Assert.Equal(AccountErrors.PasswordTooShort, (await Accept().HandleAsync(AcceptCommand(token), default)).Error);

        Assert.Equal(0, _unitOfWork.Commits);
        Assert.True((await Get().HandleAsync(token, default)).IsSuccess);
    }

    [Fact]
    public async Task Owner_removes_a_technician_and_the_account_is_deleted()
    {
        await Accept().HandleAsync(AcceptCommand(await InviteAndGetTokenAsync()), default);
        var technician = _staff.Items.Single(s => !s.IsOwner);
        var userId = technician.UserId!.Value;

        Assert.True((await Remove().HandleAsync(technician.Id, default)).IsSuccess);

        Assert.False(technician.IsActive);
        Assert.Equal([userId], _accounts.Deleted);
        Assert.Equal(1 + 1, _unitOfWork.Commits); // kabul + çıkarma
    }

    [Fact]
    public async Task Owner_cannot_remove_themselves()
    {
        Assert.Equal(ShopErrors.CannotRemoveOwner, (await Remove().HandleAsync(_owner.Id, default)).Error);
        Assert.Empty(_accounts.Deleted);
    }

    [Fact]
    public async Task Technician_cannot_remove_staff()
    {
        await Accept().HandleAsync(AcceptCommand(await InviteAndGetTokenAsync()), default);
        var technician = _staff.Items.Single(s => !s.IsOwner);
        _user.IsOwner = false;

        Assert.Equal(ApplicationErrors.OwnerOnly, (await Remove().HandleAsync(technician.Id, default)).Error);
        Assert.True(technician.IsActive);
    }

    [Fact]
    public async Task Staff_of_another_shop_is_not_found()
    {
        var stranger = Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Technician, "Başka Atölye").Value;
        _staff.Add(stranger);

        Assert.Equal(ShopErrors.StaffNotFound, (await Remove().HandleAsync(stranger.Id, default)).Error);
        Assert.True(stranger.IsActive);
    }

    [Fact]
    public async Task Owner_revokes_a_pending_invitation()
    {
        var token = await InviteAndGetTokenAsync();

        Assert.True((await Revoke().HandleAsync(_invitations.Items[0].Id, default)).IsSuccess);

        Assert.Equal(ShopErrors.InvalidInvitation, (await Get().HandleAsync(token, default)).Error);
    }

    [Fact]
    public async Task Revoking_unknown_invitation_is_not_found()
    {
        Assert.Equal(ApplicationErrors.InvitationNotFound, (await Revoke().HandleAsync(Guid.NewGuid(), default)).Error);
    }

    [Fact]
    public async Task Staff_page_lists_members_and_pending_invitations()
    {
        await Accept().HandleAsync(AcceptCommand(await InviteAndGetTokenAsync()), default);
        await InviteAndGetTokenAsync("zeynep@example.com");

        var overview = (await List().HandleAsync(default)).Value;

        Assert.Equal(["Ayşe Kaya", "Mehmet Demir"], overview.Members.Select(m => m.DisplayName));
        Assert.True(overview.Members[0].IsYou);
        Assert.Equal("zeynep@example.com", Assert.Single(overview.Invitations).Email);
    }

    [Fact]
    public async Task Technician_cannot_see_the_staff_page()
    {
        _user.IsOwner = false;
        Assert.Equal(ApplicationErrors.OwnerOnly, (await List().HandleAsync(default)).Error);
    }
}
