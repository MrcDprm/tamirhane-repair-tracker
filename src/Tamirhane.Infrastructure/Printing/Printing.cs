using QRCoder;
using Tamirhane.Application.Abstractions;

namespace Tamirhane.Infrastructure.Printing;

/// <summary>QRCoder (MIT) ile SVG QR kodu. Orta düzey hata düzeltme: etiket biraz çizilse de okunur.</summary>
internal sealed class QrCodeGenerator : IQrCodeGenerator
{
    public string Svg(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        // viewBox'lı SVG: boyutu sayfadaki CSS belirler.
        return new SvgQRCode(data).GetGraphic(new System.Drawing.Size(256, 256), "#1f2a30", "#ffffff", drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}

/// <summary>Takip sayfası adresi: PUBLIC_ORIGIN + /t/{kod} (sayfası #28'de).</summary>
internal sealed class TrackingLinks(Uri publicOrigin) : ITrackingLinks
{
    public Uri For(string trackingCode) => new(publicOrigin, "/t/" + Uri.EscapeDataString(trackingCode));
}
