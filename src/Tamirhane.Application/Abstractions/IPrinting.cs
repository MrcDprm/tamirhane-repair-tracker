namespace Tamirhane.Application.Abstractions;

/// <summary>QR kodu (kabul fişi ve cihaz etiketi). SVG: her boyutta keskin basılır, resim dosyası gerekmez.</summary>
public interface IQrCodeGenerator
{
    string Svg(string content);
}

/// <summary>
/// Müşterinin takip sayfasının adresi. Uygulamanın kendi adresinden (PUBLIC_ORIGIN) üretilir; isteğin Host başlığından
/// üretilseydi biri sahte bir adresi fişe bastırabilirdi.
/// </summary>
public interface ITrackingLinks
{
    Uri For(string trackingCode);
}
