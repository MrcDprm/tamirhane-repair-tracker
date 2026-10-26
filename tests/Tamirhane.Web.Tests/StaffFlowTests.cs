using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Personel daveti, rol politikası ve personel çıkarma; uygulamanın tamamı, HTTP üzerinden.
/// Her test kendi adreslerini kullanır (atölyeler testler arasında karışmasın).
/// </summary>
[Collection(WebCollection.Name)]
public sealed partial class StaffFlowTests(TamirhaneWebFactory app)
{
    private const string AccessDenied = "/account/access-denied";
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Invited_technician_joins_and_lands_in_the_panel()
    {
        var owner = await _flows.OwnerAsync("katil-sahip@example.com");

        var invited = await InviteAsync(owner, "katil-usta@example.com");
        Assert.Equal("/staff", Browser.RedirectOf(invited));
        Assert.Contains("katil-usta@example.com adresine davet gönderildi", await BodyAsync(owner, "/staff"));

        var technician = app.NewBrowser();
        var invitationPage = await BodyAsync(technician, _flows.InvitationLink("katil-usta@example.com"));
        Assert.Contains("Kaya Telefon seni davet etti", invitationPage);

        Assert.Equal("/panel", Browser.RedirectOf(await _flows.AcceptAsync(technician, "katil-usta@example.com")));
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync("/panel")).StatusCode);
        Assert.Contains("Mehmet Demir", await BodyAsync(owner, "/staff"));
    }

    [DockerFact]
    public async Task Technician_cannot_open_or_use_owner_pages()
    {
        var owner = await _flows.OwnerAsync("rol-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "rol-usta@example.com");

        Assert.StartsWith(AccessDenied, Browser.RedirectOf(await technician.GetAsync("/staff")));
        // Form sayfası açılmadığı için CSRF token'ı sahibin sayfasından değil panelin çıkış formundan alınır.
        var invite = await technician.SubmitAsync("/panel", "/staff/invite", new() { ["Invite.Email"] = "sizan@example.com" });
        Assert.StartsWith(AccessDenied, Browser.RedirectOf(invite));
        Assert.Empty(app.Emails.SentTo("sizan@example.com"));
        Assert.DoesNotContain("href=\"/staff\"", await BodyAsync(technician, "/panel")); // menüde de yok
    }

    [DockerFact]
    public async Task Signed_out_visitor_is_sent_to_login()
    {
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/staff")));
    }

    [DockerFact]
    public async Task Removed_technician_is_signed_out_on_the_next_request()
    {
        var owner = await _flows.OwnerAsync("cikar-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "cikar-usta@example.com");

        var removed = await owner.SubmitAsync("/staff", $"/staff/{StaffIdOf(await BodyAsync(owner, "/staff"))}/remove", new());

        Assert.Equal("/staff", Browser.RedirectOf(removed));
        Assert.DoesNotContain("Mehmet Demir", await BodyAsync(owner, "/staff"));
        Assert.StartsWith("/account/login", Browser.RedirectOf(await technician.GetAsync("/panel")));
        var login = await technician.SubmitAsync("/account/login", "/account/login",
            new() { ["Email"] = "cikar-usta@example.com", ["Password"] = TechnicianPassword });
        Assert.Contains("E-posta ya da şifre hatalı.", await login.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Owner_has_no_remove_button_and_cannot_remove_themselves()
    {
        var owner = await _flows.OwnerAsync("kendi-sahip@example.com");
        Assert.DoesNotContain("/remove", await BodyAsync(owner, "/staff"));

        // Butonu olmasa da istek elle gönderilebilir; kural sunucuda.
        var response = await owner.SubmitAsync("/staff", $"/staff/{await StaffIdAsync("kendi-sahip@example.com")}/remove", new());

        Assert.Equal("/staff", Browser.RedirectOf(response));
        var page = await BodyAsync(owner, "/staff");
        Assert.Contains("Atölyenin sahibi kendini çıkaramaz.", page);
        Assert.Contains("Ayşe Kaya", page);
    }

    [DockerFact]
    public async Task Invitation_link_works_once_and_revoked_link_is_invalid()
    {
        var owner = await _flows.OwnerAsync("link-sahip@example.com");
        await InviteAsync(owner, "link-usta@example.com");
        var link = _flows.InvitationLink("link-usta@example.com");
        await _flows.AcceptAsync(app.NewBrowser(), "link-usta@example.com");

        Assert.Contains("Davet linki geçersiz", await BodyAsync(app.NewBrowser(), link));

        await InviteAsync(owner, "geri-usta@example.com");
        var revokeAction = RevokePattern().Match(await BodyAsync(owner, "/staff")).Groups[1].Value;
        Assert.Equal("/staff", Browser.RedirectOf(await owner.SubmitAsync("/staff", revokeAction, new())));
        Assert.Contains("Davet linki geçersiz", await BodyAsync(app.NewBrowser(), _flows.InvitationLink("geri-usta@example.com")));
    }

    [DockerFact]
    public async Task Tampered_link_is_invalid()
    {
        Assert.Contains("Davet linki geçersiz", await BodyAsync(app.NewBrowser(), "/invitation?token=uydurma"));
        Assert.Contains("Davet linki geçersiz", await BodyAsync(app.NewBrowser(), "/invitation"));
    }

    [DockerFact]
    public async Task Weak_password_is_explained_and_the_link_stays_usable()
    {
        var owner = await _flows.OwnerAsync("zayif-sahip@example.com");
        await InviteAsync(owner, "zayif-usta@example.com");

        var weak = await _flows.AcceptAsync(app.NewBrowser(), "zayif-usta@example.com", "password123");

        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        Assert.Contains("sızıntı listelerinde çok sık geçiyor", await weak.Content.ReadAsStringAsync());
        Assert.Equal("/panel", Browser.RedirectOf(await _flows.AcceptAsync(app.NewBrowser(), "zayif-usta@example.com")));
    }

    [DockerFact]
    public async Task Address_with_an_account_gets_a_clear_message()
    {
        var owner = await _flows.OwnerAsync("iki-sahip@example.com");
        await _flows.OwnerAsync("iki-usta@example.com"); // bu adres başka atölyenin sahibi
        await InviteAsync(owner, "iki-usta@example.com");

        var response = await _flows.AcceptAsync(app.NewBrowser(), "iki-usta@example.com");

        Assert.Contains("zaten bir Tamirhane hesabı var", await response.Content.ReadAsStringAsync());
    }

    private static string StaffIdOf(string staffPage) => RemovePattern().Match(staffPage).Groups[1].Value;

    /// <summary>Hesabın personel kimliği; sahibin satırında buton olmadığı için sayfadan değil veritabanından.</summary>
    private async Task<Guid> StaffIdAsync(string email)
    {
        Guid id = default;
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            id = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.Id).SingleAsync();
        });
        return id;
    }

    [GeneratedRegex(@"action=""/staff/([0-9a-f-]{36})/remove""")]
    private static partial Regex RemovePattern();

    [GeneratedRegex(@"action=""(/staff/invitations/[0-9a-f-]{36}/revoke)""")]
    private static partial Regex RevokePattern();
}
