using Tamirhane.Domain.Shops;

namespace Tamirhane.Domain.Tests.Shops;

public class ShopTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void New_shop_gets_default_warranty_and_vat()
    {
        var shop = Shop.Create("  Kaya   Telefon  ", "tr", Now).Value;

        Assert.Equal("Kaya Telefon", shop.Name);
        Assert.Equal(90, shop.WarrantyDays);
        Assert.Equal(20m, shop.DefaultVatRate);
        Assert.Equal("tr", shop.Locale);
        Assert.Equal(Now, shop.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" K ")]
    public void Shop_name_must_have_at_least_two_characters(string name)
    {
        Assert.Equal(ShopErrors.InvalidName, Shop.Create(name, "tr", Now).Error);
    }

    [Fact]
    public void Shop_name_has_a_length_limit()
    {
        Assert.Equal(ShopErrors.InvalidName, Shop.Create(new string('a', Shop.MaxNameLength + 1), "tr", Now).Error);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("TR")]
    [InlineData("")]
    public void Only_turkish_and_english_are_supported(string locale)
    {
        Assert.Equal(ShopErrors.InvalidLocale, Shop.Create("Kaya Telefon", locale, Now).Error);
    }

    [Fact]
    public void Owner_staff_is_linked_to_shop_and_user()
    {
        var shopId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var staff = Staff.Create(shopId, userId, StaffRole.Owner, " Ayşe  Kaya ").Value;

        Assert.Equal(shopId, staff.ShopId);
        Assert.Equal(userId, staff.UserId);
        Assert.Equal("Ayşe Kaya", staff.DisplayName);
        Assert.True(staff.IsOwner);
    }

    [Fact]
    public void Staff_needs_a_display_name()
    {
        Assert.Equal(ShopErrors.InvalidDisplayName, Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Technician, " ").Error);
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        Assert.Equal(ShopErrors.InvalidRole, Staff.Create(Guid.NewGuid(), Guid.NewGuid(), (StaffRole)7, "Ayşe Kaya").Error);
    }
}
