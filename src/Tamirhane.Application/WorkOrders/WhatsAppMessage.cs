using Tamirhane.Application.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>
/// "WhatsApp'tan bildir" (#32): teknisyen tek tıkla müşterinin WhatsApp sohbetini hazır mesajla açar, kendisi gönderir.
/// Mesajı Tamirhane göndermez (WhatsApp Business API ücretli); wa.me linki telefondaki ya da bilgisayardaki WhatsApp'ı açar.
/// Metin e-posta ve tarayıcı bildirimiyle aynı cümleler, dil atölyenin müşteri dili, sonunda takip linki.
/// </summary>
public static class WhatsAppMessage
{
    /// <summary>
    /// wa.me linki. Telefon kayıtta uluslararası biçimde (+905321234567); wa.me başında + olmadan sadece rakam ister
    /// (905321234567). Mesaj adresin sorgu kısmında, kodlanmış olarak (Türkçe harfler, satır sonları, link).
    /// </summary>
    public static Uri Link(string phoneE164, string text) =>
        new($"https://wa.me/{Digits(phoneE164)}?text={Uri.EscapeDataString(text)}");

    public static string Text(string customerName, string device, string number, WorkOrderStatus status, string locale, Uri trackingLink)
    {
        var (_, lead) = StatusTexts.For(status, locale);
        var first = FirstName(customerName);
        return locale == "en"
            ? $"Hello {first},\n{lead}\n{device}, work order {number}\nTrack your repair: {trackingLink.AbsoluteUri}"
            : $"Merhaba {first},\n{lead}\n{device}, iş emri {number}\nOnarımı takip et: {trackingLink.AbsoluteUri}";
    }

    private static string Digits(string phoneE164)
    {
        var digits = new string(phoneE164.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 8 or > 15) // E.164 en fazla 15 hane
            throw new ArgumentException("Telefon uluslararası biçimde değil.", nameof(phoneE164));
        return digits;
    }

    // Sadece ilk ad: "Ayşe Nur Kaya" → "Ayşe". Mesaj resmî değil, takip sayfasıyla aynı "sen" dili.
    private static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
}
