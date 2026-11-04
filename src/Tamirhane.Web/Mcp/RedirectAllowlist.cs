namespace Tamirhane.Web.Mcp;

/// <summary>
/// Kayıtta kabul edilen yönlendirme adresleri. Kayıt herkese açık (RFC 7591) olduğu için adres serbest olsaydı biri
/// "Claude" adıyla istemci kaydedip kodu kendi sitesine yönlendirebilirdi; izin ekranında güzel bir ad görmek kullanıcıyı
/// yanıltırdı. Sadece bilinen istemcilerin sabit adresleri ve bilgisayardaki istemciler için loopback (RFC 8252).
/// </summary>
public static class RedirectAllowlist
{
    private static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "https://claude.ai/api/mcp/auth_callback",
        "https://claude.com/api/mcp/auth_callback",
        "https://vscode.dev/redirect",
        "https://chatgpt.com/connector_platform_oauth_redirect",
    };

    public static bool IsAllowed(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            return false;
        if (Exact.Contains(uri.AbsoluteUri))
            return true;
        // Masaüstü ve IDE istemcileri (Claude Code, Cursor...) kodu kendi bilgisayarlarındaki geçici bir portta alır
        // (RFC 8252). Kod kullanıcının kendi bilgisayarına gider; oradan ancak o bilgisayarda çalışan bir program alabilir.
        return uri.Scheme == Uri.UriSchemeHttp && uri.Host is "127.0.0.1" or "[::1]" or "localhost";
    }
}
