using Tamirhane.Infrastructure.Printing;

namespace Tamirhane.Infrastructure.Tests.Printing;

/// <summary>QR çıktısı ve takip linki. Docker gerekmez.</summary>
public sealed class PrintingTests
{
    [Fact]
    public void Qr_code_is_a_plain_scalable_svg()
    {
        var svg = new QrCodeGenerator().Svg("https://tamirhane.test/t/7K3M9QX2HFDA");

        Assert.Contains("<svg", svg);
        Assert.Contains("viewBox", svg); // boyutu sayfadaki CSS belirler
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Same_text_gives_the_same_code_and_different_text_a_different_one()
    {
        var qr = new QrCodeGenerator();
        Assert.Equal(qr.Svg("https://tamirhane.test/t/A"), qr.Svg("https://tamirhane.test/t/A"));
        Assert.NotEqual(qr.Svg("https://tamirhane.test/t/A"), qr.Svg("https://tamirhane.test/t/B"));
    }

    [Fact]
    public void Tracking_link_uses_the_public_origin()
    {
        var links = new TrackingLinks(new Uri("https://tamirhane.miracdeprem.com/"));

        Assert.Equal("https://tamirhane.miracdeprem.com/t/7K3M9QX2HFDA", links.For("7K3M9QX2HFDA").AbsoluteUri);
    }
}
