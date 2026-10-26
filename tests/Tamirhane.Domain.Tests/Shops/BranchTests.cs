using Tamirhane.Domain.Shops;

namespace Tamirhane.Domain.Tests.Shops;

public class BranchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(3));
    private static readonly Guid ShopId = Guid.NewGuid();

    [Fact]
    public void Branch_belongs_to_a_shop_with_tidy_name_and_address()
    {
        var branch = Branch.Create(ShopId, "  Kadıköy   Şubesi ", " Moda Cd.\n No: 12 ", Now).Value;

        Assert.Equal(ShopId, branch.ShopId);
        Assert.Equal("Kadıköy Şubesi", branch.Name);
        Assert.Equal("Moda Cd. No: 12", branch.Address);
        Assert.Equal(Now, branch.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Address_is_optional(string? address)
    {
        Assert.Null(Branch.Create(ShopId, "Merkez", address, Now).Value.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" K ")]
    public void Name_must_have_at_least_two_characters(string name)
    {
        Assert.Equal(ShopErrors.InvalidBranchName, Branch.Create(ShopId, name, null, Now).Error);
    }

    [Fact]
    public void Name_and_address_have_length_limits()
    {
        Assert.Equal(ShopErrors.InvalidBranchName,
            Branch.Create(ShopId, new string('a', Branch.MaxNameLength + 1), null, Now).Error);
        Assert.Equal(ShopErrors.InvalidBranchAddress,
            Branch.Create(ShopId, "Merkez", new string('a', Branch.MaxAddressLength + 1), Now).Error);
    }

    [Fact]
    public void Failed_update_changes_nothing()
    {
        var branch = Branch.Create(ShopId, "Merkez", "Moda Cd.", Now).Value;

        Assert.Equal(ShopErrors.InvalidBranchName, branch.Update("", "Yeni adres").Error);

        Assert.Equal(("Merkez", "Moda Cd."), (branch.Name, branch.Address));
    }

    [Fact]
    public void Update_can_clear_the_address()
    {
        var branch = Branch.Create(ShopId, "Merkez", "Moda Cd.", Now).Value;

        Assert.True(branch.Update("Kadıköy", null).IsSuccess);

        Assert.Equal(("Kadıköy", (string?)null), (branch.Name, branch.Address));
    }

    [Theory]
    [InlineData("tr", "Merkez")]
    [InlineData("en", "Main branch")]
    public void First_branch_is_named_in_the_shop_language(string locale, string name)
    {
        Assert.Equal(name, Branch.DefaultName(locale));
    }

    [Fact]
    public void Staff_moves_to_another_branch_of_the_same_shop()
    {
        var first = Branch.Create(ShopId, "Merkez", null, Now).Value;
        var second = Branch.Create(ShopId, "Kadıköy", null, Now).Value;
        var staff = Staff.Create(ShopId, Guid.NewGuid(), StaffRole.Technician, "Mehmet Demir", first.Id).Value;

        Assert.True(staff.MoveTo(second).IsSuccess);

        Assert.Equal(second.Id, staff.BranchId);
    }

    [Fact]
    public void Staff_cannot_move_to_a_branch_of_another_shop()
    {
        var own = Branch.Create(ShopId, "Merkez", null, Now).Value;
        var foreign = Branch.Create(Guid.NewGuid(), "Başka Dükkân", null, Now).Value;
        var staff = Staff.Create(ShopId, Guid.NewGuid(), StaffRole.Technician, "Mehmet Demir", own.Id).Value;

        Assert.Equal(ShopErrors.BranchNotFound, staff.MoveTo(foreign).Error);
        Assert.Equal(own.Id, staff.BranchId);
    }
}
