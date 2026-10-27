using Tamirhane.Application.Tests.Fakes;
using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>"WhatsApp'tan bildir": wa.me linki, uluslararası numara, durumun hazır mesajı ve takip linki (TR/EN).</summary>
public class WhatsAppMessageTests
{
    private static readonly Uri Tracking = new("https://tamirhane.test/t/7K3M9QX2HFDA");

    [Theory]
    [InlineData("+905321234567", "905321234567")]
    [InlineData("+4915112345678", "4915112345678")] // yurt dışı numara olduğu gibi
    public void Link_uses_the_international_number_without_plus(string phone, string digits)
    {
        var link = WhatsAppMessage.Link(phone, "Merhaba");

        Assert.Equal($"https://wa.me/{digits}?text=Merhaba", link.AbsoluteUri);
    }

    [Fact]
    public void Message_is_encoded_in_the_link_and_decodes_back_unchanged()
    {
        var text = WhatsAppMessage.Text("Ayşe Nur Kaya", "Apple iPhone 13", "2026-0042", WorkOrderStatus.Ready, "tr", Tracking);

        var link = WhatsAppMessage.Link("+905321234567", text);

        Assert.DoesNotContain(" ", link.AbsoluteUri);
        Assert.DoesNotContain("\n", link.AbsoluteUri);
        Assert.Equal(text, Uri.UnescapeDataString(link.Query["?text=".Length..]));
    }

    [Fact]
    public void Turkish_message_greets_by_first_name_and_ends_with_the_tracking_link()
    {
        var text = WhatsAppMessage.Text("Ayşe Nur Kaya", "Apple iPhone 13", "2026-0042", WorkOrderStatus.Ready, "tr", Tracking);

        Assert.Equal(
            "Merhaba Ayşe,\nCihazın hazır. Fişinle gelip teslim alabilirsin.\nApple iPhone 13, iş emri 2026-0042\n" +
            "Onarımı takip et: https://tamirhane.test/t/7K3M9QX2HFDA", text);
    }

    [Fact]
    public void English_shops_send_an_english_message()
    {
        var text = WhatsAppMessage.Text("John Smith", "Apple iPhone 13", "2026-0042", WorkOrderStatus.AwaitingApproval, "en", Tracking);

        Assert.StartsWith("Hello John,\nThe repair quote is ready.", text);
        Assert.EndsWith("Track your repair: https://tamirhane.test/t/7K3M9QX2HFDA", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("+90")]
    [InlineData("+1234567890123456")] // 16 hane: E.164'ten uzun
    public void A_number_that_is_not_international_is_refused(string phone)
    {
        Assert.Throws<ArgumentException>(() => WhatsAppMessage.Link(phone, "x"));
    }

    [Fact]
    public async Task Work_order_page_offers_the_link_for_the_current_status()
    {
        var shop = new TestShop();
        var id = await shop.ReceiveAsync("0532 123 45 67");
        await shop.StartDiagnosis().HandleAsync(id, default);

        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

        var link = view.WhatsAppLink!.AbsoluteUri;
        Assert.StartsWith("https://wa.me/905321234567?text=", link);
        var text = Uri.UnescapeDataString(view.WhatsAppLink.Query["?text=".Length..]);
        Assert.Contains("Cihazın inceleniyor.", text);
        Assert.EndsWith($"/t/{view.TrackingCode}", text);
    }

    [Fact]
    public async Task Work_order_page_uses_the_shop_language()
    {
        var shop = new TestShop(locale: "en");
        var id = await shop.ReceiveAsync();

        var view = (await shop.GetWorkOrder().HandleAsync(id, default)).Value;

        Assert.StartsWith("Hello Ayşe,", Uri.UnescapeDataString(view.WhatsAppLink!.Query["?text=".Length..]));
    }
}
