namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// Hesap e-postalarının ayarları.
/// PublicOrigin: e-posta linklerinin başı (PUBLIC_ORIGIN). İsteğin Host başlığı kullanılmaz; saldırgan Host'u
/// değiştirip kendi sitesine giden bir şifre sıfırlama linkini kurbanın e-postasına gönderttirebilir.
/// ResendApiKey boşsa e-postalar gönderilmez, loga yazılır (geliştirme).
/// </summary>
public sealed record AccountOptions(Uri PublicOrigin, string? ResendApiKey, string EmailFrom)
{
    public const string DefaultFrom = "Tamirhane <tamirhane@miracdeprem.com>";

    public static Uri ParseOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query)
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new InvalidOperationException(
                "PUBLIC_ORIGIN geçersiz: \"https://alan.adi\" biçiminde olmalı (http sadece localhost için).");
        return uri;
    }
}
