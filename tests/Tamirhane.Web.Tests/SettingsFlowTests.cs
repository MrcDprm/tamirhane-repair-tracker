using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Ayarlar sayfası, şube ekleme/düzenleme ve menüdeki şube seçici; HTTP üzerinden.</summary>
[Collection(WebCollection.Name)]
public sealed partial class SettingsFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Owner_updates_shop_settings()
    {
        var owner = await _flows.OwnerAsync("ayar-sahip@example.com");
        var page = await BodyAsync(owner, "/settings");
        Assert.Contains("value=\"90\"", page);
        Assert.Contains("value=\"20\"", page); // KDV kültürden bağımsız: "20,00" değil

        var saved = await UpdateSettingsAsync(owner, "Kaya Teknik", "180", "10.5", "en");

        Assert.Equal("/settings", Browser.RedirectOf(saved));
        page = await BodyAsync(owner, "/settings");
        Assert.Contains("Ayarlar kaydedildi", page);
        Assert.Contains("value=\"Kaya Teknik\"", page);
        Assert.Contains("value=\"180\"", page);
        Assert.Contains("value=\"10.5\"", page);
    }

    [DockerFact]
    public async Task Invalid_settings_are_explained_on_the_form()
    {
        var owner = await _flows.OwnerAsync("ayar-hata@example.com");

        var response = await UpdateSettingsAsync(owner, "Kaya Telefon", "90", "20.125", "tr");

        Assert.Contains("en fazla iki ondalıklı olmalı", await response.Content.ReadAsStringAsync());
        Assert.Contains("value=\"20\"", await BodyAsync(owner, "/settings"));
    }

    [DockerFact]
    public async Task Technician_cannot_open_or_post_settings()
    {
        var owner = await _flows.OwnerAsync("ayar-rol-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "ayar-rol-usta@example.com");

        Assert.StartsWith("/account/access-denied", Browser.RedirectOf(await technician.GetAsync("/settings")));
        var post = await technician.SubmitAsync("/panel", "/settings", new()
        {
            ["Shop.Name"] = "Ele Geçti", ["Shop.WarrantyDays"] = "0", ["Shop.DefaultVatRate"] = "0", ["Shop.Locale"] = "tr",
        });
        Assert.StartsWith("/account/access-denied", Browser.RedirectOf(post));
        Assert.Contains("value=\"Kaya Telefon\"", await BodyAsync(owner, "/settings"));
    }

    [DockerFact]
    public async Task Single_branch_shop_never_sees_branch_selection()
    {
        var owner = await _flows.OwnerAsync("tek-sube@example.com");
        var technician = await _flows.TechnicianAsync(owner, "tek-sube-usta@example.com");

        Assert.DoesNotContain("branch-switcher", await BodyAsync(owner, "/panel"));
        Assert.DoesNotContain("branch-switcher", await BodyAsync(owner, "/staff"));
        Assert.DoesNotContain("branch-switcher", await BodyAsync(technician, "/panel"));
        Assert.Contains("Merkez", await BodyAsync(owner, "/settings")); // ilk şube kayıtta açıldı
    }

    [DockerFact]
    public async Task Second_branch_brings_the_switcher_and_staff_can_switch()
    {
        var owner = await _flows.OwnerAsync("iki-sube@example.com");
        var technician = await _flows.TechnicianAsync(owner, "iki-sube-usta@example.com");

        Assert.Equal("/settings", Browser.RedirectOf(await AddBranchAsync(owner, "Kadıköy", "Moda Cd. 12")));

        var panel = await BodyAsync(technician, "/panel");
        Assert.Contains("branch-switcher", panel);
        Assert.Matches(SelectedOption("Merkez"), panel);

        var kadikoy = BranchOption("Kadıköy").Match(panel).Groups[1].Value;
        var switched = await technician.SubmitAsync("/panel", "/branch/switch", new() { ["branchId"] = kadikoy, ["returnUrl"] = "/panel" });

        Assert.Equal("/panel", Browser.RedirectOf(switched));
        Assert.Matches(SelectedOption("Kadıköy"), await BodyAsync(technician, "/panel"));
        Assert.Matches(SelectedOption("Merkez"), await BodyAsync(owner, "/panel")); // sahibin şubesi değişmedi
    }

    [DockerFact]
    public async Task Switching_to_another_shops_branch_changes_nothing()
    {
        var owner = await _flows.OwnerAsync("sube-sizma@example.com");
        await AddBranchAsync(owner, "Kadıköy", null);
        var other = await _flows.OwnerAsync("sube-baska@example.com");
        await AddBranchAsync(other, "Başka Şube", null);
        var foreign = BranchOption("Başka Şube").Match(await BodyAsync(other, "/panel")).Groups[1].Value;

        var response = await owner.SubmitAsync("/panel", "/branch/switch", new() { ["branchId"] = foreign, ["returnUrl"] = "https://kotu.site" });

        Assert.Equal("/panel", Browser.RedirectOf(response)); // dış adrese yönlendirilmez
        var panel = await BodyAsync(owner, "/panel");
        Assert.DoesNotContain("Başka Şube", panel);
        Assert.Matches(SelectedOption("Merkez"), panel);
    }

    [DockerFact]
    public async Task Owner_renames_a_branch_and_bad_name_is_explained()
    {
        var owner = await _flows.OwnerAsync("sube-ad@example.com");
        var id = UpdateBranchAction().Match(await BodyAsync(owner, "/settings")).Groups[1].Value;

        await owner.SubmitAsync("/settings", $"/settings/branches/{id}", new() { ["Name"] = "Kadıköy Merkez", ["Address"] = "Moda Cd." });
        var page = await BodyAsync(owner, "/settings");
        Assert.Contains("Şube güncellendi.", page);
        Assert.Contains("value=\"Kadıköy Merkez\"", page);

        await owner.SubmitAsync("/settings", $"/settings/branches/{id}", new() { ["Name"] = "K" });
        page = await BodyAsync(owner, "/settings");
        Assert.Contains("Şube adı 2 ile 100 karakter arasında olmalı.", page);
        Assert.Contains("value=\"Kadıköy Merkez\"", page);
    }

    private static Task<HttpResponseMessage> UpdateSettingsAsync(Browser owner, string name, string warranty, string vat, string locale) =>
        owner.SubmitAsync("/settings", "/settings", new()
        {
            ["Shop.Name"] = name, ["Shop.WarrantyDays"] = warranty, ["Shop.DefaultVatRate"] = vat, ["Shop.Locale"] = locale,
        });

    private static Task<HttpResponseMessage> AddBranchAsync(Browser owner, string name, string? address) =>
        owner.SubmitAsync("/settings", "/settings/branches", new() { ["NewBranch.Name"] = name, ["NewBranch.Address"] = address ?? "" });

    private static Regex SelectedOption(string name) => new($"<option value=\"[0-9a-f-]{{36}}\" selected=\"selected\">{Regex.Escape(name)}</option>");

    private static Regex BranchOption(string name) => new($"<option value=\"([0-9a-f-]{{36}})\"[^>]*>{Regex.Escape(name)}</option>");

    [GeneratedRegex(@"action=""/settings/branches/([0-9a-f-]{36})""")]
    private static partial Regex UpdateBranchAction();
}
