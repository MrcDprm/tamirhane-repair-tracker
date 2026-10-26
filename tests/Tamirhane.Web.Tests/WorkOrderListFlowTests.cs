using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>İş emri listesi ve hızlı arama: süzgeçler, arama türleri, tek sonuçta yönlendirme ve kiracı ayrımı; HTTP üzerinden.</summary>
[Collection(WebCollection.Name)]
public sealed partial class WorkOrderListFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Open_list_shows_number_device_complaint_customer_status_and_waiting_time()
    {
        var owner = await _flows.OwnerAsync("liste@example.com");
        var order = await ReceiveAsync(owner, "Selin Yılmaz", "0532 201 01 01", "Samsung", "Galaxy S23", "Şarj olmuyor");

        var list = await BodyAsync(owner, "/work-orders");

        foreach (var expected in new[] { $"href=\"{order}\"", "Samsung Galaxy S23", "Şarj olmuyor", "Selin Yılmaz", "Teslim alındı", "bugün" })
            Assert.Contains(expected, list);
        Assert.Matches(@"<a aria-current=""page"" href=""/work-orders"">", list); // menüde İş emirleri seçili
    }

    [DockerFact]
    public async Task Search_by_name_phone_imei_or_number_with_one_result_opens_the_work_order()
    {
        var owner = await _flows.OwnerAsync("arama@example.com");
        var order = await ReceiveAsync(owner, "Kemal Öztürk", "0532 202 02 02", "Apple", "iPhone 12", "Hoparlör cızırtılı", imei: "490154203237518");
        await ReceiveAsync(owner, "Derya Koç", "0533 303 03 03", "Xiaomi", "Redmi Note 12", "Kamera bulanık");
        var number = NumberPattern().Match(await BodyAsync(owner, order)).Value;

        foreach (var query in new[] { "kemal", "Öztürk", "0532 202 02 02", "5322020202", "49015 4203 237518", number, number[5..].TrimStart('0'), "iphone 12" })
            Assert.Equal(order, Browser.RedirectOf(await owner.GetAsync("/work-orders?q=" + Uri.EscapeDataString(query))));
    }

    [DockerFact]
    public async Task Search_with_several_results_lists_them_and_finds_closed_work_orders_too()
    {
        var owner = await _flows.OwnerAsync("coklu@example.com");
        var first = await ReceiveAsync(owner, "Ahmet Demir", "0532 204 04 04", "Apple", "iPhone 11", "Pil şişti");
        var second = await ReceiveAsync(owner, "Ahmet Kurt", "0532 214 14 14", "Apple", "iPad Air", "Ekran kırık");
        await StepAsync(owner, first, new() { ["Step"] = "Cancel", ["Reason"] = "Müşteri vazgeçti" });

        var found = await owner.GetAsync("/work-orders?q=ahmet");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var body = await found.Content.ReadAsStringAsync();
        Assert.Contains($"href=\"{first}\"", body);
        Assert.Contains($"href=\"{second}\"", body);
        Assert.Contains("iptal", body);

        // Açık süzgeçte iptal edilen iş görünmez, kapananlarda görünür.
        var open = await BodyAsync(owner, "/work-orders");
        Assert.DoesNotContain($"href=\"{first}\"", open);
        Assert.Contains($"href=\"{second}\"", open);
        var closed = await BodyAsync(owner, "/work-orders?filter=Closed");
        Assert.Contains($"href=\"{first}\"", closed);
        Assert.DoesNotContain($"href=\"{second}\"", closed);
    }

    [DockerFact]
    public async Task Status_filters_pick_the_matching_work_orders()
    {
        var owner = await _flows.OwnerAsync("suzgec@example.com");
        var waiting = await ReceiveAsync(owner, "Ece Şahin", "0532 205 05 05", "Huawei", "P30", "Açılmıyor");
        var diagnosing = await ReceiveAsync(owner, "Ece Tunç", "0532 215 15 15", "Huawei", "MatePad", "Dokunmatik yok");
        await StepAsync(owner, diagnosing, new() { ["Step"] = "StartDiagnosis" });

        var inRepair = await BodyAsync(owner, "/work-orders?filter=InRepair");
        Assert.DoesNotContain($"href=\"{waiting}\"", inRepair);
        Assert.DoesNotContain($"href=\"{diagnosing}\"", inRepair);
        Assert.Contains("Bu süzgeçte iş emri yok", inRepair);

        var overdue = await BodyAsync(owner, "/work-orders?filter=Overdue");
        Assert.DoesNotContain($"href=\"{waiting}\"", overdue); // bugün açıldı, 30 günü geçmedi

        var all = await BodyAsync(owner, "/work-orders?filter=All");
        Assert.Contains($"href=\"{waiting}\"", all);
        Assert.Contains($"href=\"{diagnosing}\"", all);
        Assert.Contains("Arıza tespiti", all);
    }

    [DockerFact]
    public async Task Empty_states_say_what_to_do()
    {
        var owner = await _flows.OwnerAsync("bos-liste@example.com");

        Assert.Contains("Tezgâhta açık iş yok", await BodyAsync(owner, "/work-orders"));
        var missing = await BodyAsync(owner, "/work-orders?q=" + Uri.EscapeDataString("<b>Yok</b>"));
        Assert.Contains("için iş emri yok", missing);
        Assert.Contains("&lt;b&gt;Yok&lt;/b&gt;", missing); // aranan metin kaçışlanır
        Assert.DoesNotContain("<b>Yok</b>", missing);
    }

    [DockerFact]
    public async Task Other_shops_work_orders_never_show_up_and_list_needs_sign_in()
    {
        var owner = await _flows.OwnerAsync("kiraci-liste@example.com");
        var order = await ReceiveAsync(owner, "Burak Aydın", "0532 206 06 06", "OnePlus", "Nord 3", "Wi-Fi çekmiyor");
        var number = NumberPattern().Match(await BodyAsync(owner, order)).Value;

        var attacker = await _flows.OwnerAsync("kiraci-saldirgan@example.com");
        foreach (var query in new[] { "", "?filter=All", "?q=Burak", "?q=" + Uri.EscapeDataString("0532 206 06 06"), "?q=" + number })
        {
            var response = await attacker.GetAsync("/work-orders" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(order, await response.Content.ReadAsStringAsync());
        }

        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/work-orders")));
    }

    private static async Task<string> ReceiveAsync(Browser browser, string name, string phone, string brand, string model,
        string complaint, string? imei = null)
    {
        var response = await browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = name, ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = brand, ["Input.Model"] = model,
            ["Input.Imei"] = imei ?? "", ["Input.Complaint"] = complaint,
        });
        return Browser.RedirectOf(response);
    }

    private static async Task StepAsync(Browser browser, string order, Dictionary<string, string> fields)
    {
        var response = await browser.SubmitAsync(order, order + "/steps", fields);
        Assert.Equal(order, Browser.RedirectOf(response));
    }

    [GeneratedRegex(@"\d{4}-\d{4}")]
    private static partial Regex NumberPattern();
}
