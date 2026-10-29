using Tamirhane.Domain.Shops;

namespace Tamirhane.Domain.Tests.Shops;

public class ShopTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Only_a_demo_shop_is_marked_as_demo()
    {
        Assert.False(Shop.Create("Kaya Telefon", "tr", Now).Value.IsDemo);

        var demo = Shop.CreateDemo("Demo Telefon", "en", Now).Value;
        Assert.True(demo.IsDemo);
        Assert.Equal(("Demo Telefon", "en", 90), (demo.Name, demo.Locale, demo.WarrantyDays));
    }

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
    public void Settings_are_updated_together()
    {
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;

        Assert.True(shop.UpdateSettings(" Kaya  Teknik ", 180, 10m, "en").IsSuccess);

        Assert.Equal(("Kaya Teknik", 180, 10m, "en"), (shop.Name, shop.WarrantyDays, shop.DefaultVatRate, shop.Locale));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Shop.MaxWarrantyDays, 100)]
    [InlineData(30, 1.5)]
    public void Boundary_settings_are_accepted(int warrantyDays, double vat)
    {
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;
        Assert.True(shop.UpdateSettings("Kaya Telefon", warrantyDays, (decimal)vat, "tr").IsSuccess);
    }

    [Theory]
    [InlineData(-1, 20, "shop.invalid_warranty_days")]
    [InlineData(Shop.MaxWarrantyDays + 1, 20, "shop.invalid_warranty_days")]
    [InlineData(90, -0.01, "shop.invalid_vat_rate")]
    [InlineData(90, 100.01, "shop.invalid_vat_rate")]
    [InlineData(90, 20.125, "shop.invalid_vat_rate")]
    public void Out_of_range_settings_are_rejected_and_nothing_changes(int warrantyDays, double vat, string code)
    {
        var shop = Shop.Create("Kaya Telefon", "tr", Now).Value;

        Assert.Equal(code, shop.UpdateSettings("Yeni Ad", warrantyDays, (decimal)vat, "en").Error!.Code);

        Assert.Equal(("Kaya Telefon", 90, 20m, "tr"), (shop.Name, shop.WarrantyDays, shop.DefaultVatRate, shop.Locale));
    }

    [Fact]
    public void Owner_staff_is_linked_to_shop_and_user()
    {
        var shopId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var staff = Staff.Create(shopId, userId, StaffRole.Owner, " Ayşe  Kaya ", Guid.NewGuid()).Value;

        Assert.Equal(shopId, staff.ShopId);
        Assert.Equal(userId, staff.UserId);
        Assert.Equal("Ayşe Kaya", staff.DisplayName);
        Assert.True(staff.IsOwner);
    }

    [Fact]
    public void Staff_needs_a_display_name()
    {
        Assert.Equal(ShopErrors.InvalidDisplayName, Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Technician, " ", Guid.NewGuid()).Error);
    }

    [Fact]
    public void Removed_technician_keeps_the_record_but_loses_the_account_link()
    {
        var technician = Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Technician, "Mehmet Demir", Guid.NewGuid()).Value;

        Assert.True(technician.Remove(Now).IsSuccess);

        Assert.False(technician.IsActive);
        Assert.Equal(Now, technician.RemovedAt);
        Assert.Null(technician.UserId);
        Assert.Equal("Mehmet Demir", technician.DisplayName);
    }

    [Fact]
    public void Owner_cannot_be_removed()
    {
        var owner = Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Owner, "Ayşe Kaya", Guid.NewGuid()).Value;

        Assert.Equal(ShopErrors.CannotRemoveOwner, owner.Remove(Now).Error);
        Assert.True(owner.IsActive);
    }

    [Fact]
    public void Removed_staff_cannot_be_removed_again()
    {
        var technician = Staff.Create(Guid.NewGuid(), Guid.NewGuid(), StaffRole.Technician, "Mehmet Demir", Guid.NewGuid()).Value;
        technician.Remove(Now);

        Assert.Equal(ShopErrors.StaffNotFound, technician.Remove(Now.AddMinutes(1)).Error);
    }

    [Fact]
    public void Unknown_role_is_rejected()
    {
        Assert.Equal(ShopErrors.InvalidRole, Staff.Create(Guid.NewGuid(), Guid.NewGuid(), (StaffRole)7, "Ayşe Kaya", Guid.NewGuid()).Error);
    }
}
