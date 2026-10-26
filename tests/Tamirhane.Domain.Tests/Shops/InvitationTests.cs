using System.Security.Cryptography;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Domain.Tests.Shops;

public class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.FromHours(3));
    private static readonly Guid ShopId = Guid.NewGuid();

    private static (Invitation Invitation, string Token) Invite(string email = "mehmet@example.com") =>
        Invitation.Create(ShopId, email, Guid.NewGuid(), Now).Value;

    [Fact]
    public void Invitation_is_valid_for_seven_days()
    {
        var (invitation, _) = Invite("  Mehmet@Example.COM ");

        Assert.Equal("mehmet@example.com", invitation.Email);
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.True(invitation.IsPending(Now.AddDays(7).AddSeconds(-1)));
        Assert.False(invitation.IsPending(Now.AddDays(7)));
    }

    [Fact]
    public void Only_the_hash_of_the_token_is_stored()
    {
        var (invitation, token) = Invite();

        Assert.Equal(32, invitation.TokenHash.Length);
        Assert.DoesNotContain(token, Convert.ToBase64String(invitation.TokenHash));
        Assert.Equal(invitation.TokenHash, Invitation.HashToken(token));
    }

    [Fact]
    public void Every_invitation_gets_a_different_token()
    {
        Assert.NotEqual(Invite().Token, Invite().Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kisa")]
    [InlineData("AAAA")]
    [InlineData("bu+token/gecersiz==")]
    public void Malformed_token_has_no_hash(string? token)
    {
        Assert.Null(Invitation.HashToken(token));
    }

    [Fact]
    public void Truncated_token_has_no_hash()
    {
        var (_, token) = Invite();
        Assert.Null(Invitation.HashToken(token[..^4]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("mehmet")]
    [InlineData("@example.com")]
    [InlineData("mehmet@")]
    [InlineData("mehmet@@example.com")]
    [InlineData("meh met@example.com")]
    public void Email_must_look_like_an_address(string email)
    {
        Assert.Equal(ShopErrors.InvalidEmail, Invitation.Create(ShopId, email, Guid.NewGuid(), Now).Error);
    }

    [Fact]
    public void Email_has_a_length_limit()
    {
        var email = new string('a', Invitation.MaxEmailLength) + "@example.com";
        Assert.Equal(ShopErrors.InvalidEmail, Invitation.Create(ShopId, email, Guid.NewGuid(), Now).Error);
    }

    [Fact]
    public void Invitation_can_be_accepted_once()
    {
        var (invitation, _) = Invite();

        Assert.True(invitation.Accept(Now.AddHours(1)).IsSuccess);
        Assert.Equal(Now.AddHours(1), invitation.AcceptedAt);
        Assert.Equal(ShopErrors.InvalidInvitation, invitation.Accept(Now.AddHours(2)).Error);
    }

    [Fact]
    public void Expired_invitation_cannot_be_accepted()
    {
        var (invitation, _) = Invite();
        Assert.Equal(ShopErrors.InvalidInvitation, invitation.Accept(Now.AddDays(7)).Error);
    }

    [Fact]
    public void Revoked_invitation_cannot_be_accepted()
    {
        var (invitation, _) = Invite();

        invitation.Revoke(Now.AddMinutes(5));

        Assert.Equal(Now.AddMinutes(5), invitation.RevokedAt);
        Assert.Equal(ShopErrors.InvalidInvitation, invitation.Accept(Now.AddMinutes(6)).Error);
    }

    [Fact]
    public void Revoking_an_accepted_invitation_changes_nothing()
    {
        var (invitation, _) = Invite();
        invitation.Accept(Now);

        invitation.Revoke(Now.AddMinutes(1));

        Assert.Null(invitation.RevokedAt);
    }

    [Fact]
    public void Token_hash_uses_sha256()
    {
        var (invitation, token) = Invite();
        Assert.Equal(SHA256.HashData(System.Buffers.Text.Base64Url.DecodeFromChars(token)), invitation.TokenHash);
    }
}
