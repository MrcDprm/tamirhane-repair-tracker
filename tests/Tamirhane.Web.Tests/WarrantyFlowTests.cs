using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Garanti dönüşü (#34) HTTP üzerinden: teslim edilmiş işten yeni kayıt, iki yönlü bağ, işçilik ücretsiz önerilir.</summary>
[Collection(WebCollection.Name)]
public sealed class WarrantyFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Delivered_order_opens_a_linked_warranty_order_with_free_labor_suggested()
    {
        var owner = await _flows.OwnerAsync("garanti-akis@example.com");
        var previous = await DeliveredAsync(owner, "0532 921 01 01");
        Assert.Contains(">Garanti kapsamında yeni kayıt<", await BodyAsync(owner, previous));

        var response = await owner.SubmitAsync(previous + "/warranty", previous + "/warranty", new()
        {
            ["Input.Complaint"] = "Değişen ekranda dokunmatik yine çalışmıyor",
        });
        var current = Browser.RedirectOf(response);
        Assert.NotEqual(previous, current);

        var page = await BodyAsync(owner, current);
        Assert.Contains("Garanti kapsamında geri geldi. Önceki iş emri:", page);
        Assert.Contains($"href=\"{previous}\"", page);

        await Step(owner, current, "StartDiagnosis"); // teklif formu inceleme sırasında açılır
        page = await BodyAsync(owner, current);
        Assert.Contains("Garanti kapsamında: işçilik ücretsiz önerildi.", page);
        Assert.Matches("id=\"free-price\"[^>]*value=\"0\"", page);

        var back = await BodyAsync(owner, previous);
        Assert.Contains("Garanti kapsamında geri geldi:", back);
        Assert.Contains($"href=\"{current}\"", back);
    }

    [DockerFact]
    public async Task Complaint_is_required()
    {
        var owner = await _flows.OwnerAsync("garanti-bos@example.com");
        var previous = await DeliveredAsync(owner, "0532 921 02 02");

        var response = await owner.SubmitAsync(previous + "/warranty", previous + "/warranty", new() { ["Input.Complaint"] = "" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Müşterinin şikâyetini yaz.", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task An_open_order_offers_no_warranty_record()
    {
        var owner = await _flows.OwnerAsync("garanti-acik@example.com");
        var order = await ReceiveAsync(owner, "0532 921 03 03");

        Assert.DoesNotContain("Garanti kapsamında yeni kayıt", await BodyAsync(owner, order));
        Assert.Equal(order, Browser.RedirectOf(await owner.GetAsync(order + "/warranty")));
    }

    private static async Task<string> DeliveredAsync(Browser owner, string phone)
    {
        var order = await ReceiveAsync(owner, phone);
        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady", "Deliver" })
            await Step(owner, order, step);
        return order;
    }

    private static Task<HttpResponseMessage> Step(Browser owner, string order, string step) =>
        owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = step });
}
