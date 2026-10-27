using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>"WhatsApp'tan bildir" (#32) iş emri sayfasında: wa.me linki, güncel durumun mesajı, htmx adımından sonra da güncel.</summary>
[Collection(WebCollection.Name)]
public sealed partial class WhatsAppFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Work_order_page_opens_whatsapp_with_the_status_and_the_tracking_link()
    {
        var owner = await _flows.OwnerAsync("whatsapp-sayfa@example.com");
        var order = await ReceiveAsync(owner, "0532 811 01 01");

        var page = await BodyAsync(owner, order);
        var (link, text) = WhatsApp(page);

        Assert.StartsWith("https://wa.me/905328110101?text=", link);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", page);
        Assert.StartsWith("Merhaba Zeynep,\nCihazın atölyede.", text);
        Assert.Matches(@"Onarımı takip et: https?://\S+/t/[0-9A-Z]{12}$", text);
    }

    [DockerFact]
    public async Task After_a_step_the_message_tells_the_new_status()
    {
        var owner = await _flows.OwnerAsync("whatsapp-adim@example.com");
        var order = await ReceiveAsync(owner, "0532 811 02 02");

        // Sayfa yenilenmeden (htmx) adım ilerler; dönen parçadaki link yeni durumu söylemeli.
        var response = await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" },
            new() { ["HX-Request"] = "true" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (_, text) = WhatsApp(await response.Content.ReadAsStringAsync());
        Assert.Contains("Cihazın inceleniyor.", text);
    }

    private static (string Link, string Text) WhatsApp(string html)
    {
        var link = WebUtility.HtmlDecode(WhatsAppLink().Match(html).Groups[1].Value);
        Assert.False(string.IsNullOrEmpty(link), "Sayfada WhatsApp linki yok.");
        return (link, Uri.UnescapeDataString(new Uri(link).Query["?text=".Length..]));
    }

    [GeneratedRegex("href=\"(https://wa\\.me/[^\"]+)\"")]
    private static partial Regex WhatsAppLink();
}
