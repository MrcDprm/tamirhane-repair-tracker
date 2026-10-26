using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tamirhane.Domain.WorkOrders;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Cihaz kabulü HTTP üzerinden: telefonla arama, yeni ve kayıtlı müşteri, doğrulama, overposting, kiracı ayrımı.</summary>
[Collection(WebCollection.Name)]
public sealed partial class IntakeFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task New_customer_is_received_and_the_work_order_page_opens()
    {
        var owner = await _flows.OwnerAsync("kabul-yeni@example.com");

        var search = await BodyAsync(owner, "/work-orders/new?phone=0532 111 22 33");
        Assert.Contains("kayıtlı müşteri yok", search);

        var response = await ReceiveAsync(owner, "0532 111 22 33", NewCustomer());

        var page = await BodyAsync(owner, Browser.RedirectOf(response));
        Assert.Matches(@"İş emri \d{4}-0001 açıldı", page);
        Assert.Contains("Zeynep Arslan", page);
        Assert.Contains("0532 111 22 33", page);
        Assert.Contains("490154203237518", page);
        Assert.Contains("Teslim alındı", page);
        Assert.Contains("Fişi yazdır", page);
        Assert.Contains("Cihaz şifresi kayıtlı", page);
        Assert.DoesNotContain("4826", page); // cihaz şifresinin kendisi sayfada yok
    }

    [DockerFact]
    public async Task Returning_customer_is_found_by_phone_and_the_same_device_is_reused()
    {
        var owner = await _flows.OwnerAsync("kabul-tekrar@example.com");
        await ReceiveAsync(owner, "0532 222 33 44", NewCustomer());

        var search = await BodyAsync(owner, "/work-orders/new?phone=" + Uri.EscapeDataString("+90 532 222 3344")); // farklı yazım, aynı numara
        Assert.Contains("<strong>Zeynep Arslan</strong>", search);
        Assert.DoesNotContain("kayıtlı müşteri yok", search);
        var customerId = Hidden("Input.CustomerId").Match(search).Groups[1].Value;
        var deviceId = DeviceRadio().Match(search).Groups[1].Value;

        var second = await ReceiveAsync(owner, "+90 532 222 3344", new()
        {
            ["Input.CustomerId"] = customerId,
            ["Input.DeviceId"] = deviceId,
            ["Input.Complaint"] = "Şarj soketi gevşek",
        });

        if (second.StatusCode != HttpStatusCode.Redirect)
            Assert.Fail(Regex.Match(await second.Content.ReadAsStringAsync(), @"notice-error[^>]*>[\s\S]{0,400}").Value);
        Assert.Matches(@"\d{4}-0002", await BodyAsync(owner, Browser.RedirectOf(second)));
        await AssertShopCountsAsync("kabul-tekrar@example.com", customers: 1, devices: 1, orders: 2);
    }

    [DockerFact]
    public async Task New_customer_without_consent_is_not_saved()
    {
        var owner = await _flows.OwnerAsync("kabul-kvkk@example.com");
        var fields = NewCustomer();
        fields.Remove("Input.KvkkConsent");

        var response = await ReceiveAsync(owner, "0532 333 44 55", fields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("onayı alınmadan yeni müşteri kaydedilemez", await response.Content.ReadAsStringAsync());
        await AssertShopCountsAsync("kabul-kvkk@example.com", customers: 0, devices: 0, orders: 0);
    }

    [DockerFact]
    public async Task Wrong_imei_and_missing_complaint_are_explained()
    {
        var owner = await _flows.OwnerAsync("kabul-hata@example.com");
        var fields = NewCustomer();
        fields["Input.Imei"] = "490154203237519"; // son hane tutmuyor

        var badImei = await (await ReceiveAsync(owner, "0532 444 55 66", fields)).Content.ReadAsStringAsync();
        Assert.Contains("IMEI 15 rakam olmalı ve son rakamı tutmalı", badImei);
        Assert.Contains("value=\"Zeynep Arslan\"", badImei); // yazılanlar kaybolmaz

        fields["Input.Imei"] = "";
        fields["Input.Complaint"] = "";
        Assert.Contains("Müşterinin şikâyetini yaz.", await (await ReceiveAsync(owner, "0532 444 55 66", fields)).Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Invalid_phone_stays_on_the_search_step()
    {
        var owner = await _flows.OwnerAsync("kabul-tel@example.com");

        var page = await BodyAsync(owner, "/work-orders/new?phone=12345");

        Assert.Contains("Telefonu 0532 123 45 67 gibi yaz.", page);
        Assert.Contains("Müşteriyi bul", page);
    }

    [DockerFact]
    public async Task Extra_form_fields_cannot_set_shop_branch_or_status()
    {
        var owner = await _flows.OwnerAsync("kabul-fazla@example.com");
        var foreignShop = Guid.NewGuid();
        var fields = NewCustomer();
        fields["Input.ShopId"] = foreignShop.ToString();
        fields["ShopId"] = foreignShop.ToString();
        fields["Input.BranchId"] = Guid.NewGuid().ToString();
        fields["Input.Status"] = nameof(WorkOrderStatus.Delivered);
        fields["Input.Number"] = "2026-9999";

        var response = await ReceiveAsync(owner, "0532 555 66 77", fields);
        var id = Guid.Parse(Browser.RedirectOf(response).Split('/')[^1]);

        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var order = await db.WorkOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == id);
            var staff = await db.Staff.IgnoreQueryFilters().SingleAsync(s => s.ShopId == order.ShopId);
            Assert.NotEqual(foreignShop, order.ShopId);
            Assert.Equal(staff.BranchId, order.BranchId);
            Assert.Equal(WorkOrderStatus.Received, order.Status);
            Assert.NotEqual("2026-9999", order.Number);
        });
    }

    [DockerFact]
    public async Task Another_shops_customer_and_work_order_are_out_of_reach()
    {
        var victim = await _flows.OwnerAsync("kabul-kurban@example.com");
        var victimOrder = Browser.RedirectOf(await ReceiveAsync(victim, "0532 666 77 88", NewCustomer()));
        var attacker = await _flows.OwnerAsync("kabul-saldirgan@example.com");

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync(victimOrder)).StatusCode);

        // Kurbanın müşteri kimliğini bilse bile o müşteriye iş emri açamaz.
        Guid victimCustomer = default;
        await app.AsShopAsync(Guid.Empty, async db => victimCustomer = await db.Customers.IgnoreQueryFilters()
            .Where(c => c.Phone == Domain.Customers.PhoneNumber.Parse("0532 666 77 88").Value).Select(c => c.Id).SingleAsync());
        var response = await ReceiveAsync(attacker, "0532 999 00 11", new()
        {
            ["Input.CustomerId"] = victimCustomer.ToString(),
            ["Input.DeviceKind"] = "Phone", ["Input.Brand"] = "Apple", ["Input.Model"] = "iPhone 13",
            ["Input.Complaint"] = "Ekran kırık",
        });
        Assert.Contains("Seçilen müşteri bulunamadı", await response.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Signed_out_visitor_is_sent_to_login()
    {
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/work-orders/new")));
    }

    private static Dictionary<string, string> NewCustomer() => new()
    {
        ["Input.CustomerName"] = "Zeynep Arslan",
        ["Input.CustomerEmail"] = "",
        ["Input.KvkkConsent"] = "true",
        ["Input.DeviceKind"] = "Phone",
        ["Input.Brand"] = "Apple",
        ["Input.Model"] = "iPhone 13",
        ["Input.Imei"] = "490154203237518",
        ["Input.Complaint"] = "Ekran kırık",
        ["Input.Condition"] = "Kasada çizik",
        ["Input.Accessories"] = "Kılıf",
        ["Input.Passcode"] = "4826",
    };

    /// <summary>Telefonla arama sayfasını açar (CSRF token'ı oradan) ve kabul formunu gönderir.</summary>
    private static Task<HttpResponseMessage> ReceiveAsync(Browser browser, string phone, Dictionary<string, string> fields)
    {
        fields["Input.Phone"] = phone;
        return browser.SubmitAsync($"/work-orders/new?phone={Uri.EscapeDataString(phone)}", "/work-orders/new", fields);
    }

    private async Task AssertShopCountsAsync(string ownerEmail, int customers, int devices, int orders)
    {
        await app.AsShopAsync(Guid.Empty, async db =>
        {
            var userId = await db.Users.Where(u => u.Email == ownerEmail).Select(u => u.Id).SingleAsync();
            var shopId = await db.Staff.IgnoreQueryFilters().Where(s => s.UserId == userId).Select(s => s.ShopId).SingleAsync();
            Assert.Equal(customers, await db.Customers.IgnoreQueryFilters().CountAsync(c => c.ShopId == shopId));
            Assert.Equal(devices, await db.Devices.IgnoreQueryFilters().CountAsync(d => d.ShopId == shopId));
            Assert.Equal(orders, await db.WorkOrders.IgnoreQueryFilters().CountAsync(o => o.ShopId == shopId));
        });
    }

    private static Regex Hidden(string name) => new($"name=\"{Regex.Escape(name)}\" value=\"([^\"]*)\"");

    [GeneratedRegex(@"name=""Input.DeviceId"" value=""([0-9a-f-]{36})""")]
    private static partial Regex DeviceRadio();
}
