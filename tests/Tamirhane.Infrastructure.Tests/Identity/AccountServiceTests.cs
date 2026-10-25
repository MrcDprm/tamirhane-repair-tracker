using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Shops;
using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Infrastructure.Tests.Identity;

/// <summary>Gerçek Identity ve PostgreSQL ile hesap açma ve hesap e-postaları.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountServiceTests(PostgresFixture db)
{
    private const string Password = "kirmizi-kalem-42";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(3));

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
    public async Task Tampered_or_foreign_confirmation_token_does_not_confirm()
    {
        using var scope = db.AsShop(Guid.Empty);
        var userId = (await scope.Accounts.CreateAsync("alti@example.com", Password, default)).Value;
        var otherId = (await scope.Accounts.CreateAsync("yedi@example.com", Password, default)).Value;
        await scope.Accounts.SendEmailConfirmationAsync(otherId, "tr", default);
        var otherToken = QueryHelpers.ParseQuery(LinkIn(db.Emails.SentTo("yedi@example.com").Single()).Query)["token"]!;

        Assert.False(await scope.Accounts.ConfirmEmailAsync(userId, "bozuk*token", default));
        Assert.False(await scope.Accounts.ConfirmEmailAsync(userId, otherToken!, default));
        Assert.False(await scope.Accounts.ConfirmEmailAsync(Guid.NewGuid(), otherToken!, default));
        Assert.False((await scope.Users.FindByIdAsync(userId.ToString()))!.EmailConfirmed);
    }

    [DockerFact]
    public async Task New_confirmation_link_goes_only_to_an_unconfirmed_account_in_its_shop_language()
    {
        var (_, userId) = await RegisterShopAsync("sekiz@example.com", "en");
        using var scope = db.AsShop(Guid.Empty);

        await scope.Accounts.ResendEmailConfirmationAsync("SEKIZ@example.com ", default);
        await scope.Accounts.ResendEmailConfirmationAsync("kayitsiz@example.com", default);

        var mail = Assert.Single(db.Emails.SentTo("sekiz@example.com"));
        Assert.Equal("Confirm your Tamirhane account", mail.Subject);
        Assert.Empty(db.Emails.SentTo("kayitsiz@example.com"));

        Assert.True(await scope.Accounts.ConfirmEmailAsync(userId, QueryHelpers.ParseQuery(LinkIn(mail).Query)["token"]!, default));
        await scope.Accounts.ResendEmailConfirmationAsync("sekiz@example.com", default);
        Assert.Single(db.Emails.SentTo("sekiz@example.com")); // doğrulanmış hesaba yeni link gitmez
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

    private static Uri LinkIn(EmailMessage mail) =>
        new(mail.Text.Split(' ', '\n', '\r').Single(w => w.StartsWith("https://")));

    private async Task<(Shop Shop, Guid UserId)> RegisterShopAsync(string email, string locale)
    {
        using var scope = db.AsShop(Guid.Empty);
        var userId = (await scope.Accounts.CreateAsync(email, Password, default)).Value;
        var shop = Shop.Create("Kaya Telefon", locale, Now).Value;
        scope.Db.AddRange(shop, Staff.Create(shop.Id, userId, StaffRole.Owner, "Ayşe Kaya").Value);
        await scope.Db.SaveChangesAsync();
        return (shop, userId);
    }
}
