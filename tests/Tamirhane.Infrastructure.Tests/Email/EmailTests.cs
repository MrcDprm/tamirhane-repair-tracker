using System.Net;
using System.Text;
using System.Text.Json;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Email;

/// <summary>E-posta kalıbı (TR/EN, kaçışlama, düz metin) ve Resend'e giden gönderen adı. Docker gerekmez.</summary>
public sealed class EmailTests
{
    private static readonly Uri Link = new("https://tamirhane.test/invitation?token=abc&x=1");
    private static readonly EmailFrom From = EmailFrom.Parse("Tamirhane <tamirhane@miracdeprem.com>");

    [Theory]
    [InlineData("demo-1a2b@demo.tamirhane.invalid", true)]
    [InlineData("X@Y.INVALID ", true)]
    [InlineData("ayse@example.com", false)]
    [InlineData("invalid@example.com", false)]
    [InlineData("ayse@invalid.com", false)]
    public void Reserved_invalid_addresses_are_never_sent(string address, bool undeliverable)
    {
        Assert.Equal(undeliverable, Tamirhane.Infrastructure.Background.EmailOutbox.IsUndeliverable(address));
    }

    [Theory]
    [InlineData("tr", "lang=\"tr\"", "Düğme çalışmazsa")]
    [InlineData("en", "lang=\"en\"", "If the button does not work")]
    public void Layout_is_written_in_the_shop_language(string locale, string lang, string help)
    {
        var mail = EmailLayout.Build("a@example.com", locale, "Konu", ["Paragraf"], new EmailAction("Aç", Link), "Not");

        Assert.Contains(lang, mail.Html);
        Assert.Contains(help, mail.Html);
    }

    [Fact]
    public void Every_value_is_html_escaped()
    {
        var mail = EmailLayout.Build("a@example.com", "tr", "<b>Konu</b>",
            ["<script>alert(1)</script> Kaya & Oğulları"], new EmailAction("<i>Aç</i>", Link), "\"not\"");

        Assert.DoesNotContain("<script>", mail.Html);
        Assert.DoesNotContain("<i>", mail.Html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; Kaya &amp; Oğulları", mail.Html);
        Assert.Contains("href=\"https://tamirhane.test/invitation?token=abc&amp;x=1\"", mail.Html);
    }

    [Fact]
    public void Plain_text_version_has_the_paragraphs_and_the_link()
    {
        var mail = EmailLayout.Build("a@example.com", "tr", "Konu", ["Birinci", "İkinci"], new EmailAction("Aç", Link), "Not");

        Assert.Equal(string.Join(Environment.NewLine, "Birinci", "", "İkinci", "", $"Aç: {Link.AbsoluteUri}", "", "Not"), mail.Text);
        Assert.DoesNotContain("<", mail.Text);
    }

    [Theory]
    [InlineData("tr", "Çağlar Teknik seni Tamirhane'ye davet etti", "26 Ekim 2026 12:00")]
    [InlineData("en", "Çağlar Teknik invited you to Tamirhane", "26 October 2026 12:00")]
    public void Invitation_shows_the_expiry_in_turkey_time_and_language(string locale, string subject, string expires)
    {
        // Veritabanından UTC okunur: 09:00 UTC = 12:00 Türkiye.
        var invitation = new StaffInvitationEmail("usta@example.com", "tok", "Çağlar Teknik", "Ayşe Kaya", locale,
            new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero));

        var mail = AccountEmails.Invitation(invitation, Link);

        Assert.Equal(subject, mail.Subject);
        Assert.Contains(expires, mail.Text);
    }

    [Fact]
    public void Email_from_setting_is_parsed_and_bad_setting_stops_the_app()
    {
        Assert.Equal(new EmailFrom("Tamirhane", "tamirhane@miracdeprem.com"), From);
        Assert.Throws<InvalidOperationException>(() => EmailFrom.Parse("tamirhane@miracdeprem.com")); // ad yok
        Assert.Throws<InvalidOperationException>(() => EmailFrom.Parse("Tamirhane <adres-yok>"));
    }

    [Theory]
    [InlineData(null, "\"Tamirhane\" <tamirhane@miracdeprem.com>")]
    [InlineData("Çağlar Teknik Servis", "\"Çağlar Teknik Servis\" <tamirhane@miracdeprem.com>")]
    [InlineData("Kaya\r\nBcc: kurban@example.com", "\"Kaya Bcc: kurban@example.com\" <tamirhane@miracdeprem.com>")]
    [InlineData("\"Kaya\" <sahte@kotu.site>", "\"Kaya sahte@kotu.site\" <tamirhane@miracdeprem.com>")]
    [InlineData("   ", "\"Tamirhane\" <tamirhane@miracdeprem.com>")]
    public void Sender_name_cannot_add_headers_or_change_the_address(string? name, string expected)
    {
        Assert.Equal(expected, From.Format(name));
    }

    [Fact]
    public void Long_sender_name_is_shortened()
    {
        var formatted = From.Format(new string('ş', 200));
        Assert.Equal(EmailFrom.MaxNameLength + "\"\" <tamirhane@miracdeprem.com>".Length, formatted.Length);
    }

    [Fact]
    public async Task Resend_receives_the_turkish_sender_name_intact()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.test/") }, From);

        await sender.SendAsync(new EmailMessage("musteri@example.com", "Cihazınız hazır", "metin", "<p>html</p>", "Çağlar Teknik"), default);

        Assert.Equal("utf-8", handler.ContentType!.CharSet);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("\"Çağlar Teknik\" <tamirhane@miracdeprem.com>", json.RootElement.GetProperty("from").GetString());
        Assert.Equal("Cihazınız hazır", json.RootElement.GetProperty("subject").GetString());
        Assert.Equal("musteri@example.com", json.RootElement.GetProperty("to")[0].GetString());
    }

    [Fact]
    public async Task Rejected_email_throws_without_the_response_text()
    {
        var handler = new CapturingHandler(HttpStatusCode.UnprocessableEntity, "{\"message\":\"musteri@example.com is invalid\"}");
        var sender = new ResendEmailSender(new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.test/") }, From);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync(new EmailMessage("musteri@example.com", "Konu", "metin", "<p>html</p>"), default));

        Assert.DoesNotContain("@", ex.Message);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
    }

    private sealed class CapturingHandler(HttpStatusCode status, string response = "{}") : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public System.Net.Http.Headers.MediaTypeHeaderValue? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = Encoding.UTF8.GetString(await request.Content!.ReadAsByteArrayAsync(ct));
            ContentType = request.Content.Headers.ContentType;
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
