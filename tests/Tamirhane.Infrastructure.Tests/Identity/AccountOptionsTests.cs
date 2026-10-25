using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Identity;

public class AccountOptionsTests
{
    [Theory]
    [InlineData("https://tamirhane.miracdeprem.com")]
    [InlineData("https://tamirhane.miracdeprem.com/")]
    [InlineData("http://localhost:5080")]
    public void Valid_public_origins_are_accepted(string value)
    {
        Assert.Equal(value.TrimEnd('/') + "/", AccountOptions.ParseOrigin(value).AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tamirhane.miracdeprem.com")]
    [InlineData("http://tamirhane.miracdeprem.com")] // e-posta linki şifresiz bağlantıyla gitmesin
    [InlineData("https://tamirhane.miracdeprem.com/app")]
    [InlineData("https://tamirhane.miracdeprem.com/?x=1")]
    public void Invalid_public_origins_stop_the_app(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => AccountOptions.ParseOrigin(value));
    }
}
