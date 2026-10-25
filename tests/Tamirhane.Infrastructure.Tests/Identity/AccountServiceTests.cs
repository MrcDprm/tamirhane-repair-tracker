using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Accounts;

namespace Tamirhane.Infrastructure.Tests.Identity;

/// <summary>Gerçek Identity ve PostgreSQL ile hesap açma ve hesap e-postaları.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountServiceTests(PostgresFixture db)
{
    private const string Password = "kirmizi-kalem-42";

    [DockerFact]
    public async Task Confirmation_link_comes_from_public_origin_and_confirms_the_account()
    {
        using var scope = db.AsShop(Guid.Empty);
        var userId = (await scope.Accounts.CreateAsync(" Usta.Bir@Example.com ", Password, default)).Value;

        await scope.Accounts.SendEmailConfirmationAsync(userId, "tr", default);

        var mail = Assert.Single(db.Emails.SentTo("usta.bir@example.com"));
        var link = new Uri(mail.Text.Split(' ', '\n', '\r').Single(w => w.StartsWith("https://")));
        Assert.Equal("https://tamirhane.test/account/confirm-email", link.GetLeftPart(UriPartial.Path));

        var query = QueryHelpers.ParseQuery(link.Query);
        Assert.Equal(userId.ToString(), query["userId"].ToString());
        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(query["token"]!));
        var user = (await scope.Users.FindByIdAsync(userId.ToString()))!;
        Assert.False(user.EmailConfirmed);
        Assert.True((await scope.Users.ConfirmEmailAsync(user, token)).Succeeded);
    }

    [DockerFact]
    public async Task Same_email_in_other_case_is_taken()
    {
        using var scope = db.AsShop(Guid.Empty);
        await scope.Accounts.CreateAsync("iki@example.com", Password, default);

        var again = await scope.Accounts.CreateAsync("IKI@example.com", Password, default);

        Assert.Equal(AccountErrors.EmailTaken, again.Error);
    }

    [DockerFact]
    public async Task Weak_password_on_a_taken_email_reports_the_password_not_the_email()
    {
        using var scope = db.AsShop(Guid.Empty);
        await scope.Accounts.CreateAsync("uc@example.com", Password, default);

        var again = await scope.Accounts.CreateAsync("uc@example.com", "kisa1", default);

        Assert.Equal(AccountErrors.PasswordTooShort, again.Error);
    }

    [DockerFact]
    public async Task Already_registered_email_links_to_login_and_password_reset()
    {
        using var scope = db.AsShop(Guid.Empty);

        await scope.Accounts.SendAlreadyRegisteredAsync("dort@example.com", "en", default);

        var mail = Assert.Single(db.Emails.SentTo("dort@example.com"));
        Assert.Contains("https://tamirhane.test/account/login", mail.Text);
        Assert.Contains("https://tamirhane.test/account/forgot-password", mail.Text);
        Assert.Equal("You already have a Tamirhane account", mail.Subject);
    }

    [DockerFact]
    public async Task Signing_keys_are_kept_in_the_database()
    {
        using var scope = db.AsShop(Guid.Empty);
        var userId = (await scope.Accounts.CreateAsync("bes@example.com", Password, default)).Value;

        await scope.Accounts.SendEmailConfirmationAsync(userId, "tr", default);

        // Bellekte kalsaydı sunucu her yeniden başladığında oturumlar ve gönderilmiş linkler geçersiz olurdu.
        Assert.True(await scope.Db.DataProtectionKeys.AnyAsync(k => k.Xml!.Contains("<key ")));
    }
}
