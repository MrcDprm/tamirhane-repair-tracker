using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>İş emri sayfası HTTP üzerinden: sıradaki adım, geçersiz geçiş, htmx ile kısmi güncelleme, iptal, notlar.</summary>
[Collection(WebCollection.Name)]
public sealed class WorkOrderFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Order_moves_from_received_to_delivered_one_step_at_a_time()
    {
        var owner = await _flows.OwnerAsync("is-akis@example.com");
        var order = await ReceiveAsync(owner, "0532 101 01 01");

        var page = await BodyAsync(owner, order);
        Assert.Contains(">İncelemeye başla<", page);
        Assert.DoesNotContain(">Onarım bitti, teslime hazır<", page); // geçersiz geçiş görünmez

        foreach (var step in new[] { "StartDiagnosis", "StartRepair", "MarkReady", "Deliver" })
            Assert.Equal(order, Browser.RedirectOf(await StepAsync(owner, order, step)));

        page = await BodyAsync(owner, order);
        Assert.Contains("Cihaz teslim edildi.", page);
        Assert.Contains("Garanti", page);
        Assert.DoesNotContain("Sıradaki adım", page);
        foreach (var stamp in new[] { "Teslim alındı", "Arıza tespiti", "Onarımda", "Teslime hazır", "Teslim edildi" })
            Assert.Contains(stamp, page);
    }

    [DockerFact]
    public async Task Invalid_step_sent_by_hand_is_refused_and_nothing_changes()
    {
        var owner = await _flows.OwnerAsync("is-gecersiz@example.com");
        var order = await ReceiveAsync(owner, "0532 202 02 02");

        await StepAsync(owner, order, "MarkReady");

        var page = await BodyAsync(owner, order);
        Assert.Contains("Bu işlem iş emrinin şu anki durumunda yapılamaz", page);
        Assert.Contains(">İncelemeye başla<", page);
        Assert.DoesNotContain(">Onarımda<", page);
    }

    [DockerFact]
    public async Task Htmx_step_returns_only_the_work_order_part_with_a_freshly_pressed_stamp()
    {
        var owner = await _flows.OwnerAsync("is-htmx@example.com");
        var order = await ReceiveAsync(owner, "0532 303 03 03");

        var response = await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" },
            new() { ["HX-Request"] = "true" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<div id=\"work-order\">", html.TrimStart());
        Assert.DoesNotContain("<html", html);
        Assert.Contains("stamp is-current is-pressed\">Arıza tespiti<", html);
        Assert.Contains("Durum güncellendi: Arıza tespiti.", html);
    }

    [DockerFact]
    public async Task Cancel_needs_a_reason_that_then_shows_in_the_history()
    {
        var owner = await _flows.OwnerAsync("is-iptal@example.com");
        var order = await ReceiveAsync(owner, "0532 404 04 04");

        await StepAsync(owner, order, "Cancel");
        Assert.Contains("İptalin nedenini yaz", await BodyAsync(owner, order));

        await StepAsync(owner, order, "Cancel", new() { ["Reason"] = "Müşteri onarımdan vazgeçti" });
        var page = await BodyAsync(owner, order);
        Assert.Contains("stamp stamp-warn is-current", page);
        Assert.Contains("Müşteri onarımdan vazgeçti", page);
        Assert.DoesNotContain("Sıradaki adım", page);
    }

    [DockerFact]
    public async Task Workshop_and_customer_notes_land_in_separate_lists()
    {
        var owner = await _flows.OwnerAsync("is-not@example.com");
        var order = await ReceiveAsync(owner, "0532 505 05 05");

        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "Anakart nemli", ["VisibleToCustomer"] = "false" });
        await owner.SubmitAsync(order, order + "/notes", new() { ["Text"] = "Parça Perşembe gelecek", ["VisibleToCustomer"] = "true" });

        var page = await BodyAsync(owner, order);
        var customerList = page[page.IndexOf("note-list is-customer", StringComparison.Ordinal)..];
        var workshopList = page[page.IndexOf("class=\"note-list\"", StringComparison.Ordinal)..page.IndexOf("Müşteriye görünen notlar", StringComparison.Ordinal)];
        Assert.Contains("Anakart nemli", workshopList);
        Assert.DoesNotContain("Parça Perşembe gelecek", workshopList);
        Assert.Contains("Parça Perşembe gelecek", customerList);
        Assert.DoesNotContain("Anakart nemli", customerList);
    }

    [DockerFact]
    public async Task Technician_can_move_the_order_forward()
    {
        var owner = await _flows.OwnerAsync("is-usta-sahip@example.com");
        var technician = await _flows.TechnicianAsync(owner, "is-usta@example.com");
        var order = await ReceiveAsync(technician, "0532 606 06 06");

        Assert.Equal(order, Browser.RedirectOf(await StepAsync(technician, order, "StartDiagnosis")));
        Assert.Contains("stamp is-current", await BodyAsync(technician, order));
    }

    [DockerFact]
    public async Task Another_shop_cannot_touch_the_order()
    {
        var owner = await _flows.OwnerAsync("is-kurban@example.com");
        var order = await ReceiveAsync(owner, "0532 707 07 07");
        var attacker = await _flows.OwnerAsync("is-saldirgan@example.com");

        var response = await attacker.SubmitAsync("/panel", order + "/steps", new() { ["Step"] = "Cancel", ["Reason"] = "Sabotaj" });
        var note = await attacker.SubmitAsync("/panel", order + "/notes", new() { ["Text"] = "Sızma" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, note.StatusCode);
        Assert.Contains(">İncelemeye başla<", await BodyAsync(owner, order));
        Assert.DoesNotContain("Sızma", await BodyAsync(owner, order));
    }

    private static Task<HttpResponseMessage> StepAsync(Browser browser, string order, string step, Dictionary<string, string>? extra = null)
    {
        var fields = extra ?? [];
        fields["Step"] = step;
        return browser.SubmitAsync(order, order + "/steps", fields);
    }
}
