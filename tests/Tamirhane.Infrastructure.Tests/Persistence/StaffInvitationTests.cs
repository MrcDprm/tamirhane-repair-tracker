using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Shops;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Persistence;

/// <summary>
/// Davet ve personel çıkarma gerçek Identity ve PostgreSQL ile: davet oturumsuz kabul edilir ama sadece
/// kendi atölyesine personel ekleyebilir; çıkarılan personelin hesabı silinir, kaydı kalır.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StaffInvitationTests(PostgresFixture db)
{
    private const string Password = "mavi-tornavida-17";

    [DockerFact]
    public async Task Invitation_link_finds_the_shop_without_a_session_but_other_shops_cannot_see_it()
    {
        var shop = await RegisterAsync("davet-sahip@example.com");
        var token = await InviteAsync(shop, "davet-usta@example.com");

        using var anonymous = db.AsShop(Guid.Empty);
        var found = await anonymous.Get<IInvitationRepository>().FindByTokenHashAsync(Invitation.HashToken(token)!, default);
        Assert.Equal(("Kaya Telefon", "davet-usta@example.com"), (found!.ShopName, found.Invitation.Email));

        using var other = db.AsShop(Guid.NewGuid());
        Assert.Null(await other.Get<IInvitationRepository>().GetAsync(found.Invitation.Id, default));
        Assert.Empty(await other.Get<IInvitationRepository>().ListPendingAsync(DateTimeOffset.UtcNow, null, default));
    }

    [DockerFact]
    public async Task Email_contains_a_single_use_link_from_the_public_origin()
    {
        var shop = await RegisterAsync("link-sahip@example.com");
        var token = await InviteAsync(shop, "link-usta@example.com");

        var mail = Assert.Single(db.Emails.SentTo("link-usta@example.com"));
        Assert.Contains($"https://tamirhane.test/invitation?token={token}", mail.Text);
        Assert.Contains("Kaya Telefon", mail.Subject);

        using var check = db.AsShop(shop.ShopId);
        var stored = await check.Db.Invitations.SingleAsync();
        Assert.DoesNotContain(token, Convert.ToBase64String(stored.TokenHash)); // veritabanında sadece özet
    }

    [DockerFact]
    public async Task Accepting_without_a_session_creates_a_confirmed_technician_session()
    {
        var shop = await RegisterAsync("kabul-sahip@example.com");
        var token = await InviteAsync(shop, "kabul-usta@example.com");

        using (var anonymous = db.AsShop(Guid.Empty))
            Assert.True((await Accept(anonymous).HandleAsync(new(token, "Mehmet Demir", Password), default)).IsSuccess);

        using var check = db.AsShop(shop.ShopId);
        var user = (await check.Users.FindByEmailAsync("kabul-usta@example.com"))!;
        Assert.True(user.EmailConfirmed);
        var technician = await check.Db.Staff.SingleAsync(s => s.UserId == user.Id);
        Assert.Equal(StaffRole.Technician, technician.Role);
        Assert.Equal((await check.Db.Branches.SingleAsync()).Id, technician.BranchId); // ilk şubeye bağlanır
        Assert.NotNull((await check.Db.Invitations.SingleAsync()).AcceptedAt);

        using (var again = db.AsShop(Guid.Empty))
            Assert.Equal(ShopErrors.InvalidInvitation,
                (await Accept(again).HandleAsync(new(token, "Başka Biri", "baska-sifre-99"), default)).Error);
    }

    [DockerFact]
    public async Task Accepted_invitation_cannot_be_used_to_add_staff_to_another_shop()
    {
        var shop = await RegisterAsync("kotu-sahip@example.com");
        var victim = await RegisterAsync("kurban-sahip@example.com");
        var token = await InviteAsync(shop, "kotu-usta@example.com");

        using var anonymous = db.AsShop(Guid.Empty);
        var invitation = (await anonymous.Get<IInvitationRepository>().FindByTokenHashAsync(Invitation.HashToken(token)!, default))!.Invitation;
        invitation.Accept(DateTimeOffset.UtcNow);
        var user = new ApplicationUser { UserName = "kotu-usta@example.com", Email = "kotu-usta@example.com" };
        anonymous.Db.Users.Add(user);
        anonymous.Db.Staff.Add(Staff.Create(victim.ShopId, user.Id, StaffRole.Technician, "Sızan Usta", Guid.NewGuid()).Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.Db.SaveChangesAsync());
    }

    [DockerFact]
    public async Task Address_with_an_account_leaves_the_invitation_unused()
    {
        var shop = await RegisterAsync("dolu-sahip@example.com");
        var token = await InviteAsync(shop, "dolu-sahip@example.com"); // sahibin kendi adresi zaten kayıtlı

        using (var anonymous = db.AsShop(Guid.Empty))
            Assert.Equal(AccountErrors.EmailTaken,
                (await Accept(anonymous).HandleAsync(new(token, "Ayşe Kaya", Password), default)).Error);

        using var check = db.AsShop(shop.ShopId);
        Assert.Null((await check.Db.Invitations.SingleAsync()).AcceptedAt);
        Assert.Single(await check.Db.Staff.ToListAsync());
    }

    [DockerFact]
    public async Task Removing_a_technician_deletes_the_account_and_keeps_the_record()
    {
        var shop = await RegisterAsync("cikar-sahip@example.com");
        var token = await InviteAsync(shop, "cikar-usta@example.com");
        using (var anonymous = db.AsShop(Guid.Empty))
            await Accept(anonymous).HandleAsync(new(token, "Mehmet Demir", Password), default);

        Guid technicianId;
        using (var owner = OwnerScope(shop))
        {
            technicianId = (await owner.Db.Staff.SingleAsync(s => s.Role == StaffRole.Technician)).Id;
            var remove = new RemoveStaff(owner.Get<IStaffRepository>(), owner.Accounts, owner.UnitOfWork, owner.User, TimeProvider.System);
            Assert.True((await remove.HandleAsync(technicianId, default)).IsSuccess);
        }

        using var check = db.AsShop(shop.ShopId);
        Assert.Null(await check.Users.FindByEmailAsync("cikar-usta@example.com"));
        var record = await check.Db.Staff.SingleAsync(s => s.Id == technicianId);
        Assert.Equal(("Mehmet Demir", (Guid?)null), (record.DisplayName, record.UserId));
        Assert.NotNull(record.RemovedAt);
        Assert.DoesNotContain(await check.Get<IStaffRepository>().ListActiveAsync(default), s => s.Id == technicianId);
    }

    private sealed record RegisteredShop(Guid ShopId, Guid OwnerStaffId);

    private async Task<RegisteredShop> RegisterAsync(string ownerEmail)
    {
        using (var anonymous = db.AsShop(Guid.Empty))
        {
            var register = new RegisterShop(anonymous.Accounts, anonymous.Get<IShopRepository>(), anonymous.UnitOfWork, TimeProvider.System);
            Assert.True((await register.HandleAsync(new("Kaya Telefon", "Ayşe Kaya", ownerEmail, Password, "tr"), default)).IsSuccess);
        }

        using var lookup = db.AsShop(Guid.Empty);
        var user = (await lookup.Users.FindByEmailAsync(ownerEmail))!;
        var owner = await lookup.Db.Staff.IgnoreQueryFilters().SingleAsync(s => s.UserId == user.Id);
        return new RegisteredShop(owner.ShopId, owner.Id);
    }

    private async Task<string> InviteAsync(RegisteredShop shop, string email)
    {
        using var owner = OwnerScope(shop);
        var invite = new InviteStaff(owner.Get<IInvitationRepository>(), owner.Get<IShopRepository>(), owner.Get<IStaffRepository>(),
            owner.Accounts, owner.UnitOfWork, owner.User, TimeProvider.System);
        Assert.True((await invite.HandleAsync(email, default)).IsSuccess);

        var link = db.Emails.SentTo(email)[^1].Text.Split(' ', '\n', '\r').Single(w => w.Contains("/invitation?token="));
        return link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
    }

    private ShopScope OwnerScope(RegisteredShop shop)
    {
        var scope = db.AsShop(shop.ShopId);
        scope.Get<TestUser>().StaffId = shop.OwnerStaffId;
        return scope;
    }

    private static AcceptInvitation Accept(ShopScope scope) => new(
        scope.Get<IInvitationRepository>(), scope.Get<IStaffRepository>(), scope.Accounts, scope.UnitOfWork, TimeProvider.System);
}
