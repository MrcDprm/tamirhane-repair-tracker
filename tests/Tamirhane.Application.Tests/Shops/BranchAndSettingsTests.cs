using Microsoft.Extensions.Time.Testing;
using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Shops;
using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Shops;

public class BranchAndSettingsTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeShops _shops = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUser _user;
    private readonly FakeBranches _branches;
    private readonly FakeStaff _staff;
    private readonly Shop _shop;
    private readonly Branch _main;
    private readonly Staff _owner;

    public BranchAndSettingsTests()
    {
        _shop = Shop.Create("Kaya Telefon", "tr", _clock.GetLocalNow()).Value;
        _main = Branch.Create(_shop.Id, "Merkez", null, _clock.GetLocalNow()).Value;
        _owner = Staff.Create(_shop.Id, Guid.NewGuid(), StaffRole.Owner, "Ayşe Kaya", _main.Id).Value;
        _shops.Add(_shop, _main, _owner);
        _user = new FakeUser(_shop.Id) { StaffId = _owner.Id, BranchId = _main.Id };
        _branches = new FakeBranches(_user);
        _branches.Add(_main);
        _staff = new FakeStaff(_user);
        _staff.Add(_owner);
    }

    private AddBranch Add() => new(_branches, _unitOfWork, _user, _clock);
    private UpdateBranch Update() => new(_branches, _unitOfWork, _user);
    private ListBranches List() => new(_branches, _user);
    private SwitchBranch Switch() => new(_branches, _staff, _unitOfWork, _user);
    private GetShopSettings GetSettings() => new(_shops, _user);
    private UpdateShopSettings UpdateSettings() => new(_shops, _unitOfWork, _user);

    [Fact]
    public async Task Owner_adds_a_branch()
    {
        var id = (await Add().HandleAsync(new("Kadıköy", "Moda Cd. 12"), default)).Value;

        var branch = _branches.Items.Single(b => b.Id == id);
        Assert.Equal((_shop.Id, "Kadıköy", "Moda Cd. 12"), (branch.ShopId, branch.Name, branch.Address));
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Technician_cannot_add_or_edit_branches()
    {
        _user.IsOwner = false;

        Assert.Equal(ApplicationErrors.OwnerOnly, (await Add().HandleAsync(new("Kadıköy", null), default)).Error);
        Assert.Equal(ApplicationErrors.OwnerOnly, (await Update().HandleAsync(_main.Id, new("Yeni Ad", null), default)).Error);
        Assert.Single(_branches.Items);
        Assert.Equal("Merkez", _main.Name);
    }

    [Fact]
    public async Task Branch_count_is_capped()
    {
        for (var i = 1; i < Branch.MaxPerShop; i++)
            await Add().HandleAsync(new($"Şube {i}", null), default);

        Assert.Equal(ShopErrors.TooManyBranches, (await Add().HandleAsync(new("Fazla", null), default)).Error);
        Assert.Equal(Branch.MaxPerShop, _branches.Items.Count);
    }

    [Fact]
    public async Task Owner_renames_a_branch()
    {
        Assert.True((await Update().HandleAsync(_main.Id, new("Kadıköy Merkez", "Moda Cd."), default)).IsSuccess);
        Assert.Equal(("Kadıköy Merkez", "Moda Cd."), (_main.Name, _main.Address));
    }

    [Fact]
    public async Task Invalid_branch_name_is_rejected()
    {
        Assert.Equal(ShopErrors.InvalidBranchName, (await Add().HandleAsync(new(" ", null), default)).Error);
        Assert.Equal(ShopErrors.InvalidBranchName, (await Update().HandleAsync(_main.Id, new("", null), default)).Error);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Branch_of_another_shop_is_not_found()
    {
        var foreign = Branch.Create(Guid.NewGuid(), "Başka Dükkân", null, _clock.GetLocalNow()).Value;
        _branches.Add(foreign);

        Assert.Equal(ShopErrors.BranchNotFound, (await Update().HandleAsync(foreign.Id, new("Ele Geçti", null), default)).Error);
        Assert.Equal(ShopErrors.BranchNotFound, (await Switch().HandleAsync(foreign.Id, default)).Error);
        Assert.Equal("Başka Dükkân", foreign.Name);
        Assert.Equal(_main.Id, _owner.BranchId);
    }

    [Fact]
    public async Task Staff_switches_the_working_branch()
    {
        var second = (await Add().HandleAsync(new("Kadıköy", null), default)).Value;
        _user.IsOwner = false; // teknisyen de şube değiştirebilir

        Assert.True((await Switch().HandleAsync(second, default)).IsSuccess);

        Assert.Equal(second, _owner.BranchId);
    }

    [Fact]
    public async Task Branch_list_marks_the_current_branch()
    {
        await Add().HandleAsync(new("Kadıköy", null), default);

        var list = await List().HandleAsync(default);

        Assert.Equal(["Merkez", "Kadıköy"], list.Select(b => b.Name));
        Assert.Equal([true, false], list.Select(b => b.IsCurrent));
    }

    [Fact]
    public async Task Owner_reads_and_updates_shop_settings()
    {
        Assert.Equal(new ShopSettingsCommand("Kaya Telefon", 90, 20m, "tr"), (await GetSettings().HandleAsync(default)).Value);

        Assert.True((await UpdateSettings().HandleAsync(new("Kaya Teknik", 180, 10m, "en"), default)).IsSuccess);

        Assert.Equal(("Kaya Teknik", 180, 10m, "en"), (_shop.Name, _shop.WarrantyDays, _shop.DefaultVatRate, _shop.Locale));
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Invalid_settings_are_not_saved()
    {
        Assert.Equal(ShopErrors.InvalidWarrantyDays, (await UpdateSettings().HandleAsync(new("Kaya Telefon", -1, 20m, "tr"), default)).Error);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Technician_cannot_see_or_change_settings()
    {
        _user.IsOwner = false;

        Assert.Equal(ApplicationErrors.OwnerOnly, (await GetSettings().HandleAsync(default)).Error);
        Assert.Equal(ApplicationErrors.OwnerOnly, (await UpdateSettings().HandleAsync(new("Ele Geçti", 0, 0m, "tr"), default)).Error);
        Assert.Equal("Kaya Telefon", _shop.Name);
    }
}
