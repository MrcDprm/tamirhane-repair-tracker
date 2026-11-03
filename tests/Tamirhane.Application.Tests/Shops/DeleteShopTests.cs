using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Shops;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Shops;

public class DeleteShopTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(3));
    private const string Password = "kirmizi-kalem-42";

    private readonly FakeAccounts _accounts = new();
    private readonly FakeShops _shops = new();
    private readonly FakeEraser _eraser = new();

    [Fact]
    public async Task Owner_with_the_right_password_deletes_the_shop()
    {
        var (delete, shop) = Arrange(Shop.Create("Kaya Telefon", "tr", Now).Value, StaffRole.Owner);

        var result = await delete.HandleAsync(Password, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([shop.Id], _eraser.Deleted);
    }

    [Theory]
    [InlineData("yanlis-sifre-1")]
    [InlineData("")]
    public async Task Wrong_or_empty_password_deletes_nothing(string password)
    {
        var (delete, _) = Arrange(Shop.Create("Kaya Telefon", "tr", Now).Value, StaffRole.Owner);

        var result = await delete.HandleAsync(password, CancellationToken.None);

        Assert.Equal(DeleteShopErrors.WrongPassword, result.Error);
        Assert.Empty(_eraser.Deleted);
    }

    [Fact]
    public async Task Technician_cannot_delete_the_shop()
    {
        var (delete, _) = Arrange(Shop.Create("Kaya Telefon", "tr", Now).Value, StaffRole.Technician);

        var result = await delete.HandleAsync(Password, CancellationToken.None);

        Assert.Equal(ApplicationErrors.OwnerOnly, result.Error);
        Assert.Empty(_eraser.Deleted);
    }

    [Fact]
    public async Task Demo_shop_is_left_to_the_cleanup()
    {
        var (delete, _) = Arrange(Shop.CreateDemo("Demo Telefon", "tr", Now).Value, StaffRole.Owner);

        var result = await delete.HandleAsync(Password, CancellationToken.None);

        Assert.Equal(DeleteShopErrors.Demo, result.Error);
        Assert.Empty(_eraser.Deleted);
    }

    private (DeleteShop Delete, Shop Shop) Arrange(Shop shop, StaffRole role)
    {
        var branch = Branch.Create(shop.Id, "Merkez", null, Now).Value;
        var userId = Guid.NewGuid();
        var member = Staff.Create(shop.Id, userId, role, "Ayşe Kaya", branch.Id).Value;
        _shops.Add(shop, branch, member);
        _accounts.Passwords[userId] = Password;

        var user = new FakeUser(shop.Id, isOwner: role == StaffRole.Owner) { StaffId = member.Id };
        var staff = new FakeStaff(user);
        staff.Add(member);
        return (new DeleteShop(_shops, staff, _accounts, _eraser, user), shop);
    }

    private sealed class FakeEraser : IShopEraser
    {
        public List<Guid> Deleted { get; } = [];

        public Task DeleteAsync(Guid shopId, CancellationToken ct)
        {
            Deleted.Add(shopId);
            return Task.CompletedTask;
        }
    }
}
