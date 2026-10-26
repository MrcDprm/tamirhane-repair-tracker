using System.Globalization;
using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Söz verilen teslim günü: kabulde verilir, iş emri sayfasında değişir ya da kaldırılır; HTTP üzerinden.</summary>
[Collection(WebCollection.Name)]
public sealed class PromiseFlowTests(TamirhaneWebFactory app)
{
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Promise_given_at_intake_shows_on_the_work_order()
    {
        var owner = await _flows.OwnerAsync("soz-kabul@example.com");

        var response = await ReceiveAsync(owner, "0532 401 01 01", Today.AddDays(3).ToString("yyyy-MM-dd"));

        var page = await BodyAsync(owner, Browser.RedirectOf(response));
        Assert.Contains($"<dt>Teslim günü</dt><dd>{Today.AddDays(3).ToString("d MMMM dddd", Turkish)}</dd>", page);
        Assert.Contains($"value=\"{Today.AddDays(3):yyyy-MM-dd}\"", page);
    }

    [DockerFact]
    public async Task Past_promise_at_intake_is_explained_and_nothing_is_saved()
    {
        var owner = await _flows.OwnerAsync("soz-gecmis@example.com");

        var response = await ReceiveAsync(owner, "0532 402 02 02", Today.AddDays(-1).ToString("yyyy-MM-dd"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Teslim günü bugün ya da sonraki 180 gün içinde olmalı.", await response.Content.ReadAsStringAsync());
        Assert.Contains("Tezgâhta açık iş yok", await BodyAsync(owner, "/work-orders"));
    }

    [DockerFact]
    public async Task Promise_is_changed_and_removed_on_the_work_order_page()
    {
        var owner = await _flows.OwnerAsync("soz-degis@example.com");
        var order = Browser.RedirectOf(await ReceiveAsync(owner, "0532 403 03 03", ""));
        Assert.DoesNotContain("<dt>Teslim günü</dt>", await BodyAsync(owner, order));

        await owner.SubmitAsync(order, order + "/promise", new() { ["Date"] = Today.AddDays(5).ToString("yyyy-MM-dd") });
        var page = await BodyAsync(owner, order);
        Assert.Contains("Teslim günü kaydedildi", page);
        Assert.Contains(Today.AddDays(5).ToString("d MMMM dddd", Turkish), page);

        await owner.SubmitAsync(order, order + "/promise", new() { ["Date"] = Today.AddDays(400).ToString("yyyy-MM-dd") });
        page = await BodyAsync(owner, order);
        Assert.Contains("Teslim günü bugün ya da sonraki 180 gün içinde olmalı.", page);
        Assert.Contains(Today.AddDays(5).ToString("d MMMM dddd", Turkish), page);

        await owner.SubmitAsync(order, order + "/promise", new());
        page = await BodyAsync(owner, order);
        Assert.Contains("Teslim günü kaldırıldı.", page);
        Assert.DoesNotContain("<dt>Teslim günü</dt>", page);
    }

    [DockerFact]
    public async Task Other_shops_cannot_change_the_promise()
    {
        var owner = await _flows.OwnerAsync("soz-sahip@example.com");
        var order = Browser.RedirectOf(await ReceiveAsync(owner, "0532 404 04 04", ""));
        var attacker = await _flows.OwnerAsync("soz-saldirgan@example.com");

        var response = await attacker.SubmitAsync("/panel", order + "/promise", new() { ["Date"] = Today.AddDays(1).ToString("yyyy-MM-dd") });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<dt>Teslim günü</dt>", await BodyAsync(owner, order));
    }

    private static Task<HttpResponseMessage> ReceiveAsync(Browser browser, string phone, string promisedDate) =>
        browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", new()
        {
            ["Input.Phone"] = phone, ["Input.CustomerName"] = "Zeynep Arslan", ["Input.KvkkConsent"] = "true",
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kırık", ["Input.PromisedDate"] = promisedDate,
        });
}
