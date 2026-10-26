using System.Text.RegularExpressions;

namespace Tamirhane.Web.Tests;

/// <summary>Testlerde sık geçen kullanıcı akışları: atölye sahibi olarak giriş, teknisyen daveti ve kabulü.</summary>
public sealed partial class ShopFlows(TamirhaneWebFactory app)
{
    public const string OwnerPassword = "kirmizi-kalem-42";
    public const string TechnicianPassword = "mavi-tornavida-17";

    /// <summary>Atölye kaydı + e-posta doğrulama + giriş; sahibin tarayıcısını döner.</summary>
    public async Task<Browser> OwnerAsync(string email)
    {
        var browser = app.NewBrowser();
        await browser.SubmitAsync("/account/register", "/account/register", new()
        {
            ["ShopName"] = "Kaya Telefon", ["OwnerName"] = "Ayşe Kaya", ["Email"] = email, ["Password"] = OwnerPassword, ["Locale"] = "tr",
        });
        await browser.GetAsync(LinkIn(email, "/account/confirm-email"));
        var login = await browser.SubmitAsync("/account/login", "/account/login", new() { ["Email"] = email, ["Password"] = OwnerPassword });
        Assert.Equal("/panel", Browser.RedirectOf(login));
        return browser;
    }

    public static Task<HttpResponseMessage> InviteAsync(Browser owner, string email) =>
        owner.SubmitAsync("/staff", "/staff/invite", new() { ["Invite.Email"] = email });

    public async Task<HttpResponseMessage> AcceptAsync(Browser browser, string email, string password = TechnicianPassword)
    {
        var link = InvitationLink(email);
        var token = link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
        return await browser.SubmitAsync(link, "/invitation", new()
        {
            ["Input.Token"] = Uri.UnescapeDataString(token),
            ["Input.DisplayName"] = "Mehmet Demir",
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
        });
    }

    /// <summary>Sahip davet eder, teknisyen kabul eder; teknisyenin tarayıcısını döner.</summary>
    public async Task<Browser> TechnicianAsync(Browser owner, string email)
    {
        await InviteAsync(owner, email);
        var technician = app.NewBrowser();
        Assert.Equal("/panel", Browser.RedirectOf(await AcceptAsync(technician, email)));
        return technician;
    }

    public string InvitationLink(string email) => LinkIn(email, "/invitation");

    /// <summary>Adrese giden, verilen sayfaya işaret eden son linkin yolu ve sorgusu.</summary>
    public string LinkIn(string email, string path)
    {
        var mail = app.Emails.SentTo(email).Last(m => m.Text.Contains(path));
        return new Uri(LinkPattern().Matches(mail.Text).Single(m => m.Value.Contains(path)).Value).PathAndQuery;
    }

    public static async Task<string> BodyAsync(Browser browser, string url) =>
        await (await browser.GetAsync(url)).Content.ReadAsStringAsync();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
