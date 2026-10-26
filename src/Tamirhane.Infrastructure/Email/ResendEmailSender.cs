using System.Net.Http.Json;

namespace Tamirhane.Infrastructure.Email;

/// <summary>Resend API'si (ücretsiz katman). Anahtar RESEND_API_KEY ortam değişkeninden gelir.</summary>
internal sealed class ResendEmailSender(HttpClient http, EmailFrom from) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("emails", new
        {
            from = from.Format(message.FromName),
            to = new[] { message.To },
            subject = message.Subject,
            text = message.Text,
            html = message.Html,
        }, ct);

        // Hata metni alıcı adresini içerebilir; istisnaya sadece durum kodu yazılır.
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Resend e-postayı kabul etmedi: {(int)response.StatusCode}", null, response.StatusCode);
    }
}
