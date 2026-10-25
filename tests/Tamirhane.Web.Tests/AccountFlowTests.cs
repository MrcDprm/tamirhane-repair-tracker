using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Kayıt, doğrulama, giriş ve çıkış; uygulamanın tamamı, gerçek Identity ve PostgreSQL ile, HTTP üzerinden.
/// Her test kendi e-posta adresini kullanır (kilit ve "zaten kayıtlı" durumu testler arasında taşınmasın).
/// </summary>
[Collection(WebCollection.Name)]
public sealed partial class AccountFlowTests(TamirhaneWebFactory app)
{
    private const string Password = "kirmizi-kalem-42";
    private const string InvalidLogin = "E-posta ya da şifre hatalı.";

    [DockerFact]
    public async Task Owner_registers_confirms_the_email_and_reaches_the_panel()
    {
        var browser = app.NewBrowser();

        var registered = await RegisterAsync(browser, "sahip@example.com");
        Assert.Equal("/account/check-email", Browser.RedirectOf(registered));

        var confirmed = await (await browser.GetAsync(ConfirmationLink("sahip@example.com"))).Content.ReadAsStringAsync();
        Assert.Contains("E-posta doğrulandı", confirmed);

        Assert.Equal("/panel", Browser.RedirectOf(await LoginAsync(browser, "sahip@example.com", Password)));
        var panel = await browser.GetAsync("/panel");
        Assert.Equal(HttpStatusCode.OK, panel.StatusCode);
        Assert.Contains("sahip@example.com", await panel.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Unconfirmed_account_cannot_sign_in()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "dogrulanmamis@example.com");

        var login = await LoginAsync(browser, "dogrulanmamis@example.com", Password);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("henüz doğrulanmadı", await login.Content.ReadAsStringAsync());
        Assert.StartsWith("/account/login", Browser.RedirectOf(await browser.GetAsync("/panel")));
    }

    [DockerFact]
    public async Task Registering_a_taken_email_looks_the_same_and_warns_the_owner()
    {
        await RegisterAsync(app.NewBrowser(), "kayitli@example.com");

        var again = await RegisterAsync(app.NewBrowser(), "kayitli@example.com");

        Assert.Equal("/account/check-email", Browser.RedirectOf(again));
        var mails = app.Emails.SentTo("kayitli@example.com");
        Assert.Equal(2, mails.Count);
        Assert.Contains(mails, m => m.Subject.Contains("zaten bir Tamirhane hesabın var"));
    }

    [DockerFact]
    public async Task Common_password_is_explained_on_the_form_and_opens_no_account()
    {
        var response = await RegisterAsync(app.NewBrowser(), "zayif@example.com", "password123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("sızıntı listelerinde çok sık geçiyor", await response.Content.ReadAsStringAsync());
        Assert.Empty(app.Emails.SentTo("zayif@example.com"));
    }

    [DockerFact]
    public async Task Wrong_password_unknown_email_and_locked_account_get_the_same_answer()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "kilit@example.com");
        await browser.GetAsync(ConfirmationLink("kilit@example.com"));

        var wrong = await BodyOf(LoginAsync(browser, "kilit@example.com", "yanlis-sifre-1"));
        var unknown = await BodyOf(LoginAsync(browser, "hic-yok@example.com", Password));
        for (var i = 2; i <= 5; i++)
            await LoginAsync(browser, "kilit@example.com", $"yanlis-sifre-{i}");
        var lockedWithRightPassword = await LoginAsync(browser, "kilit@example.com", Password);

        Assert.Contains(InvalidLogin, wrong);
        Assert.Contains(InvalidLogin, unknown);
        Assert.Equal(HttpStatusCode.OK, lockedWithRightPassword.StatusCode); // panele yönlendirmedi
        Assert.Contains(InvalidLogin, await lockedWithRightPassword.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Sign_in_never_redirects_to_another_site()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "yonlendirme@example.com");
        await browser.GetAsync(ConfirmationLink("yonlendirme@example.com"));

        var response = await LoginAsync(browser, "yonlendirme@example.com", Password, returnUrl: "https://kotu.site/giris");

        Assert.Equal("/panel", Browser.RedirectOf(response));
    }

    [DockerFact]
    public async Task Panel_without_a_session_sends_to_login_and_back()
    {
        var response = await app.NewBrowser().GetAsync("/panel");

        Assert.Equal("/account/login?ReturnUrl=%2Fpanel", Browser.RedirectOf(response));
    }

    [DockerFact]
    public async Task Sign_out_ends_the_session()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "cikis@example.com");
        await browser.GetAsync(ConfirmationLink("cikis@example.com"));
        await LoginAsync(browser, "cikis@example.com", Password);

        await browser.SubmitAsync("/panel", "/account/logout", []);

        Assert.StartsWith("/account/login", Browser.RedirectOf(await browser.GetAsync("/panel")));
    }

    [DockerFact]
    public async Task Form_without_csrf_token_is_rejected()
    {
        var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "csrf@example.com",
            ["Password"] = Password,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [DockerFact]
    public async Task New_confirmation_link_request_looks_the_same_for_every_address()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "yeni-link@example.com");

        var known = await RequestNewLinkAsync(browser, "yeni-link@example.com");
        var unknown = await RequestNewLinkAsync(browser, "bilinmeyen@example.com");

        Assert.Equal("/account/check-email", Browser.RedirectOf(known));
        Assert.Equal("/account/check-email", Browser.RedirectOf(unknown));
        Assert.Equal(2, app.Emails.SentTo("yeni-link@example.com").Count);
        Assert.Empty(app.Emails.SentTo("bilinmeyen@example.com"));
    }

    [DockerFact]
    public async Task Broken_confirmation_link_shows_a_way_forward()
    {
        var page = await app.NewBrowser().GetAsync($"/account/confirm-email?userId={Guid.NewGuid()}&token=bozuk");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Yeni link iste", await page.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Forgotten_password_is_reset_by_email_and_other_devices_are_signed_out()
    {
        var laptop = app.NewBrowser();
        await RegisterAsync(laptop, "unuttum@example.com");
        await laptop.GetAsync(ConfirmationLink("unuttum@example.com"));
        await LoginAsync(laptop, "unuttum@example.com", Password);
        var phone = app.NewBrowser();

        Assert.Equal("/account/forgot-password-sent", Browser.RedirectOf(await ForgotPasswordAsync(phone, "unuttum@example.com")));
        Assert.Equal("/account/reset-password-done",
            Browser.RedirectOf(await ResetPasswordAsync(phone, "unuttum@example.com", "yeni-mavi-kalem-7")));

        // Güvenlik damgası değişti: dizüstündeki eski oturum bir sonraki istekte düşer.
        Assert.StartsWith("/account/login", Browser.RedirectOf(await laptop.GetAsync("/panel")));
        Assert.Contains(InvalidLogin, await BodyOf(LoginAsync(phone, "unuttum@example.com", Password)));
        Assert.Equal("/panel", Browser.RedirectOf(await LoginAsync(phone, "unuttum@example.com", "yeni-mavi-kalem-7")));
    }

    [DockerFact]
    public async Task Reset_link_also_confirms_an_unconfirmed_account()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "linki-kaybetti@example.com");
        await ForgotPasswordAsync(browser, "linki-kaybetti@example.com");

        await ResetPasswordAsync(browser, "linki-kaybetti@example.com", "yeni-mavi-kalem-7");

        Assert.Equal("/panel", Browser.RedirectOf(await LoginAsync(browser, "linki-kaybetti@example.com", "yeni-mavi-kalem-7")));
    }

    [DockerFact]
    public async Task Forgot_password_looks_the_same_for_an_unknown_address()
    {
        var response = await ForgotPasswordAsync(app.NewBrowser(), "hic-kaydolmadi@example.com");

        Assert.Equal("/account/forgot-password-sent", Browser.RedirectOf(response));
        Assert.Empty(app.Emails.SentTo("hic-kaydolmadi@example.com"));
    }

    [DockerFact]
    public async Task Used_reset_link_and_mismatched_passwords_are_explained_on_the_form()
    {
        var browser = app.NewBrowser();
        await RegisterAsync(browser, "iki-kez@example.com");
        await ForgotPasswordAsync(browser, "iki-kez@example.com");

        var mismatch = await BodyOf(ResetPasswordAsync(browser, "iki-kez@example.com", "yeni-mavi-kalem-7", "yeni-mavi-kalem-8"));
        await ResetPasswordAsync(browser, "iki-kez@example.com", "yeni-mavi-kalem-7");
        var reused = await BodyOf(ResetPasswordAsync(browser, "iki-kez@example.com", "baska-yesil-kalem-9"));

        Assert.Contains("İki şifre aynı değil.", mismatch);
        Assert.Contains("Bu sıfırlama linki geçersiz", reused);
    }

    private static Task<HttpResponseMessage> RegisterAsync(Browser browser, string email, string password = Password) =>
        browser.SubmitAsync("/account/register", "/account/register", new()
        {
            ["ShopName"] = "Kaya Telefon",
            ["OwnerName"] = "Ayşe Kaya",
            ["Email"] = email,
            ["Password"] = password,
            ["Locale"] = "tr",
        });

    private static Task<HttpResponseMessage> LoginAsync(Browser browser, string email, string password, string? returnUrl = null)
    {
        var fields = new Dictionary<string, string> { ["Email"] = email, ["Password"] = password };
        if (returnUrl is not null)
            fields["ReturnUrl"] = returnUrl;
        return browser.SubmitAsync("/account/login", "/account/login", fields);
    }

    private static Task<HttpResponseMessage> RequestNewLinkAsync(Browser browser, string email) =>
        browser.SubmitAsync("/account/resend-confirmation", "/account/resend-confirmation", new() { ["Email"] = email });

    private static async Task<string> BodyOf(Task<HttpResponseMessage> response) =>
        await (await response).Content.ReadAsStringAsync();

    private string ConfirmationLink(string email) => LinkIn(email, "/account/confirm-email");

    /// <summary>Adrese giden, verilen sayfaya işaret eden son linkin yolu ve sorgusu (adresin başı PUBLIC_ORIGIN).</summary>
    private string LinkIn(string email, string path)
    {
        var mail = app.Emails.SentTo(email).Last(m => m.Text.Contains(path));
        return new Uri(LinkPattern().Matches(mail.Text).Single(m => m.Value.Contains(path)).Value).PathAndQuery;
    }

    /// <summary>E-postadaki sıfırlama linkini açar ve yeni şifre formunu gönderir; gizli alanlar linkten gelir.</summary>
    private async Task<HttpResponseMessage> ResetPasswordAsync(Browser browser, string email, string password, string? confirm = null)
    {
        var link = LinkIn(email, "/account/reset-password");
        var query = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), link).Query);
        return await browser.SubmitAsync(link, "/account/reset-password", new()
        {
            ["UserId"] = query["userId"].ToString(),
            ["Token"] = query["token"].ToString(),
            ["Password"] = password,
            ["ConfirmPassword"] = confirm ?? password,
        });
    }

    private static Task<HttpResponseMessage> ForgotPasswordAsync(Browser browser, string email) =>
        browser.SubmitAsync("/account/forgot-password", "/account/forgot-password", new() { ["Email"] = email });

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex LinkPattern();
}
