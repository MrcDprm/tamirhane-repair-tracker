using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Tamirhane.Infrastructure.Email;

/// <summary>E-postadaki düğme: metni ve gittiği link.</summary>
public sealed record EmailAction(string Label, Uri Link);

/// <summary>
/// Bütün e-postaların ortak kalıbı (TR/EN): başlık, paragraflar, isteğe bağlı düğme, alt not. Her e-posta hem HTML
/// hem düz metin olarak gider (düz metni tercih eden ve spam filtresi için). Bütün değerler HTML'e kaçışlanarak
/// yazılır: atölye ya da müşteri adındaki "&lt;script&gt;" e-postada sadece metin olur.
/// Stiller satır içi; e-posta programları &lt;style&gt; bloklarını çoğu zaman siler.
/// </summary>
public static class EmailLayout
{
    private const string Ink = "#1f2328";
    private const string Muted = "#6a737d";
    private const string Accent = "#1f4e9c"; // ıstampa mavisi (#20'de tasarım sistemine taşınır)

    // Türkçe harfler olduğu gibi kalır ("&#252;" olmaz); <, >, &, " ve ' kaçışlanır.
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static EmailMessage Build(
        string to, string locale, string subject, IReadOnlyList<string> paragraphs, EmailAction? action, string footer,
        string? fromName = null)
    {
        var lang = locale == "en" ? "en" : "tr";
        var sentBy = lang == "en" ? "Sent by Tamirhane, repair shop management." : "Bu e-posta Tamirhane servis takip sistemiyle gönderildi.";
        var buttonHelp = lang == "en" ? "If the button does not work, copy this link into your browser:" : "Düğme çalışmazsa bu linki tarayıcına yapıştır:";

        return new EmailMessage(to, subject, Text(paragraphs, action, footer), Html(lang, subject, paragraphs, action, footer, sentBy, buttonHelp), fromName);
    }

    private static string Text(IReadOnlyList<string> paragraphs, EmailAction? action, string footer)
    {
        var text = new StringBuilder();
        foreach (var paragraph in paragraphs)
            text.Append(paragraph).Append("\n\n");
        if (action is not null)
            text.Append(action.Label).Append(": ").Append(action.Link.AbsoluteUri).Append("\n\n");
        return text.Append(footer).ToString().Replace("\n", Environment.NewLine);
    }

    private static string Html(
        string lang, string subject, IReadOnlyList<string> paragraphs, EmailAction? action, string footer, string sentBy, string buttonHelp)
    {
        static string E(string value) => Encoder.Encode(value);

        var body = new StringBuilder();
        foreach (var paragraph in paragraphs)
            body.Append($"""<p style="margin:0 0 16px;font-size:16px;line-height:1.5;color:{Ink}">{E(paragraph)}</p>""");
        if (action is not null)
        {
            var href = E(action.Link.AbsoluteUri);
            body.Append($"""
                <p style="margin:24px 0"><a href="{href}" style="display:inline-block;background:{Accent};color:#ffffff;text-decoration:none;font-weight:600;padding:12px 20px;border-radius:6px">{E(action.Label)}</a></p>
                <p style="margin:0 0 16px;font-size:13px;line-height:1.5;color:{Muted}">{E(buttonHelp)}<br><a href="{href}" style="color:{Accent};word-break:break-all">{href}</a></p>
                """);
        }

        return $"""
            <!doctype html>
            <html lang="{lang}">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{E(subject)}</title></head>
            <body style="margin:0;padding:0;background:#f4f5f7">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f5f7;padding:24px 12px">
            <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:8px;font-family:Arial,Helvetica,sans-serif">
            <tr><td style="padding:20px 28px;border-bottom:3px solid {Accent};font-size:18px;font-weight:700;color:{Accent};>Tamirhane</td></tr>
            <tr><td style="padding:28px">{body}<p style="margin:24px 0 0;font-size:13px;line-height:1.5;color:{Muted}">{E(footer)}</p></td></tr>
            </table>
            <p style="margin:16px 0 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:{Muted}">{E(sentBy)}</p>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }
}
