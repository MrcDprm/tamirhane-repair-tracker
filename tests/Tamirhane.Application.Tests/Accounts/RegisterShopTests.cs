using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Accounts;

public class RegisterShopTests
{
    private readonly FakeAccounts _accounts = new();
    private readonly FakeShops _shops = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 25, 6, 0, 0, TimeSpan.Zero));

    private RegisterShop Handler() => new(_accounts, _shops, _unitOfWork, _clock);

    private static RegisterShopCommand Command(string email = "ayse@example.com", string owner = "Ayşe Kaya") =>
        new("Kaya Telefon", owner, email, "kirmizi-kalem-42", "tr");

    [Fact]
    public async Task Registration_creates_shop_with_owner_and_sends_confirmation()
    {
        var result = await Handler().HandleAsync(Command(), default);

        Assert.True(result.IsSuccess);
        var (shop, branch, owner) = Assert.Single(_shops.Items);
        Assert.Equal("Kaya Telefon", shop.Name);
        Assert.Equal(shop.Id, owner.ShopId);
        Assert.Equal(StaffRole.Owner, owner.Role);
        Assert.Equal((shop.Id, "Merkez", branch.Id), (branch.ShopId, branch.Name, owner.BranchId));
        Assert.Equal(_accounts.Users["ayse@example.com"], owner.UserId);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([(owner.UserId!.Value, "tr")], _accounts.ConfirmationsSent);
    }

    [Fact]
    public async Task Taken_email_looks_like_success_and_notifies_the_address_owner()
    {
        _accounts.Users["ayse@example.com"] = Guid.NewGuid();

        var result = await Handler().HandleAsync(Command("AYSE@example.com"), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(_shops.Items);
        Assert.Equal(0, _unitOfWork.Commits);
        Assert.Empty(_accounts.ConfirmationsSent);
        Assert.Equal([("AYSE@example.com", "tr")], _accounts.AlreadyRegisteredSent);
    }

    [Fact]
    public async Task Weak_password_returns_its_error_and_saves_nothing()
    {
        _accounts.NextPasswordError = AccountErrors.PasswordTooCommon;

        var result = await Handler().HandleAsync(Command(), default);

        Assert.Equal(AccountErrors.PasswordTooCommon, result.Error);
        Assert.Empty(_shops.Items);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Invalid_owner_name_rolls_back_the_new_account()
    {
        var result = await Handler().HandleAsync(Command(owner: " "), default);

        Assert.Equal(ShopErrors.InvalidDisplayName, result.Error);
        Assert.Empty(_shops.Items);
        Assert.Equal(0, _unitOfWork.Commits); // commit yok: hesap da transaction'la birlikte geri alınır
        Assert.Empty(_accounts.ConfirmationsSent);
    }

    [Fact]
    public async Task Invalid_shop_name_stops_before_creating_an_account()
    {
        var result = await Handler().HandleAsync(Command() with { ShopName = "" }, default);

        Assert.Equal(ShopErrors.InvalidName, result.Error);
        Assert.Empty(_accounts.Users);
    }
}
