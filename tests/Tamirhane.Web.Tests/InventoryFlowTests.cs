using System.Net;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Stok ekranları HTTP üzerinden: parça ekleme, giriş ve sayım, arama, kritik stok, yetki, iş emriyle bağ.</summary>
[Collection(WebCollection.Name)]
public sealed partial class InventoryFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Owner_adds_a_part_then_receives_and_counts_stock()
    {
        var owner = await _flows.OwnerAsync("stok-akis@example.com");

        var added = await AddPartAsync(owner, "IP13-SCR", "iPhone 13 ekran paneli", "1800", "2500", "2", "1");
        var part = Browser.RedirectOf(added);
        var page = await BodyAsync(owner, part);
        Assert.Contains("iPhone 13 ekran paneli eklendi.", page);
        Assert.Contains("stock-count warn-text\">1<", page); // 1 adet, kritik seviye 2
        Assert.Contains("Stok girişi", page);

        await owner.SubmitAsync(part, part + "/receive", new() { ["quantity"] = "4", ["note"] = "Fatura 1234" });
        await owner.SubmitAsync(part, part + "/adjust", new() { ["counted"] = "4" });
        Assert.Contains("Sayım düzeltmesinin nedenini yaz", await BodyAsync(owner, part));

        await owner.SubmitAsync(part, part + "/adjust", new() { ["counted"] = "4", ["reason"] = "Biri kırıldı" });
        page = await BodyAsync(owner, part);
        Assert.Contains("stock-count\">4<", page);
        Assert.Contains("Biri kırıldı", page);
        Assert.Contains("Fatura 1234", page);
        Assert.Contains("change-minus\">−1<", page);
    }

    [DockerFact]
    public async Task List_searches_and_filters_low_stock()
    {
        var owner = await _flows.OwnerAsync("stok-liste@example.com");
        await AddPartAsync(owner, "IP13-SCR", "iPhone 13 ekran paneli", "1800", "2500", "2", "1");
        await AddPartAsync(owner, "SMS-BAT", "Samsung A54 batarya", "400", "900", "1", "5");

        var all = await BodyAsync(owner, "/inventory");
        Assert.Contains("<strong>2</strong>parça çeşidi", all);
        Assert.Contains("<strong>1</strong>kritik stokta", all);

        var search = await BodyAsync(owner, "/inventory?q=ip13");
        Assert.Contains("iPhone 13 ekran paneli", search);
        Assert.DoesNotContain("Samsung A54 batarya", search);

        var low = await BodyAsync(owner, "/inventory?low=true");
        Assert.Contains("iPhone 13 ekran paneli", low);
        Assert.DoesNotContain("Samsung A54 batarya", low);
    }

    [DockerFact]
    public async Task Technician_counts_stock_but_cannot_see_cost_or_add_parts()
    {
        var owner = await _flows.OwnerAsync("stok-rol-sahip@example.com");
        var part = Browser.RedirectOf(await AddPartAsync(owner, "USB-C", "Şarj soketi", "50", "150", "0", "2"));
        var technician = await _flows.TechnicianAsync(owner, "stok-rol-usta@example.com");

        var list = await BodyAsync(technician, "/inventory");
        Assert.Contains("Şarj soketi", list);
        Assert.DoesNotContain("Alış fiyatı", list);
        Assert.DoesNotContain("Yeni parça", list);
        Assert.DoesNotContain("₺50,00", list); // alış fiyatı (satış ₺150,00 görünür)

        var refused = await AddPartAsync(technician, "GIZLI", "Gizli parça", "1", "1", "0", "0", page: "/inventory");
        Assert.Contains("sadece atölye sahibinin işi", await refused.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Gizli parça", await BodyAsync(owner, "/inventory"));

        await technician.SubmitAsync(part, part + "/receive", new() { ["quantity"] = "3" });
        Assert.Contains("stock-count\">5<", await BodyAsync(technician, part));
    }

    [DockerFact]
    public async Task Part_used_in_a_repair_links_to_the_work_order_and_retired_part_leaves_the_quote_picker()
    {
        var owner = await _flows.OwnerAsync("stok-is@example.com");
        var part = Browser.RedirectOf(await AddPartAsync(owner, "IP13-SCR", "iPhone 13 ekran paneli", "1800", "2500", "0", "2"));
        var partId = part.Split('/')[^1];
        var order = await ReceiveAsync(owner, "0532 909 09 09");
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        await owner.SubmitAsync(order, order + "/quote-lines", new() { ["Kind"] = "Part", ["PartId"] = partId, ["Quantity"] = "1" });
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartRepair" });
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "MarkReady" });

        var page = await BodyAsync(owner, part);
        Assert.Contains("Onarımda kullanıldı", page);
        Assert.Matches(WorkOrderLink(), page);
        Assert.Contains("stock-count\">1<", page);

        await owner.SubmitAsync(part, part + "/active", new() { ["active"] = "false" });
        var second = await ReceiveAsync(owner, "0532 909 09 10");
        await owner.SubmitAsync(second, second + "/steps", new() { ["Step"] = "StartDiagnosis" });
        Assert.DoesNotContain("iPhone 13 ekran paneli (IP13-SCR)", await BodyAsync(owner, second));
        Assert.DoesNotContain(">iPhone 13 ekran paneli</a>", await BodyAsync(owner, "/inventory"));
        Assert.Contains("kullanımdan kalktı", await BodyAsync(owner, "/inventory?inactive=true"));
    }

    [DockerFact]
    public async Task Another_shops_part_is_out_of_reach()
    {
        var owner = await _flows.OwnerAsync("stok-kurban@example.com");
        var part = Browser.RedirectOf(await AddPartAsync(owner, "X1", "Kurbanın parçası", "1", "2", "0", "5"));
        var attacker = await _flows.OwnerAsync("stok-saldirgan@example.com");

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync(part)).StatusCode);
        var receive = await attacker.SubmitAsync("/inventory", part + "/receive", new() { ["quantity"] = "100" });
        Assert.Equal(HttpStatusCode.NotFound, receive.StatusCode);
        Assert.Contains("stock-count\">5<", await BodyAsync(owner, part));
    }

    private static Task<HttpResponseMessage> AddPartAsync(
        Browser browser, string sku, string name, string cost, string sale, string critical, string opening, string page = "/inventory") =>
        browser.SubmitAsync(page, "/inventory", new()
        {
            ["NewPart.Sku"] = sku, ["NewPart.Name"] = name, ["NewPart.CostPrice"] = cost, ["NewPart.SalePrice"] = sale,
            ["NewPart.CriticalLevel"] = critical, ["NewPart.OpeningQuantity"] = opening,
        });

    [GeneratedRegex(@"<a href=""/work-orders/[0-9a-f-]{36}"">İş emri \d{4}-0001</a>")]
    private static partial Regex WorkOrderLink();
}
