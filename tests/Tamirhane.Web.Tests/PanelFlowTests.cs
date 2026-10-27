using System.Net;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>Panel (#33) HTTP üzerinden: sayılar listeyi süzer, en uzun bekleyenler, ciro sadece sahibe.</summary>
[Collection(WebCollection.Name)]
public sealed class PanelFlowTests(TamirhaneWebFactory app)
{
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Empty_shop_shows_zero_counts_and_the_empty_state()
    {
        var owner = await _flows.OwnerAsync("panel-bos@example.com");

        var page = await BodyAsync(owner, "/panel");

        Assert.Contains("Tezgâhta açık iş yok", page);
        Assert.Contains("href=\"/work-orders?filter=AwaitingApproval\"", page);
        Assert.Contains("<strong>0</strong>", page);
    }

    [DockerFact]
    public async Task Counts_link_to_the_filtered_list_and_open_work_is_listed()
    {
        var owner = await _flows.OwnerAsync("panel-sayi@example.com");
        var order = await ReceiveAsync(owner, "0532 911 01 01");
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartDiagnosis" });
        await owner.SubmitAsync(order, order + "/steps", new() { ["Step"] = "StartRepair" });

        var page = await BodyAsync(owner, "/panel");

        Assert.DoesNotContain("Tezgâhta açık iş yok", page);
        Assert.Contains("href=\"/work-orders?filter=InRepair\"", page);
        Assert.Matches(@"<strong>1</strong>\s*<span>Onarımda</span>", page);
        Assert.Contains($"href=\"{order}\"", page); // en uzun bekleyenlerde
        Assert.Contains("Tüm açık işler (1)", page);

        var list = await owner.GetAsync("/work-orders?filter=InRepair");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(order, await list.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task Monthly_revenue_is_shown_to_the_owner_only()
    {
        var owner = await _flows.OwnerAsync("panel-ciro@example.com");
        var technician = await _flows.TechnicianAsync(owner, "panel-teknisyen@example.com");

        Assert.Contains("Bu ay ciro", await BodyAsync(owner, "/panel"));

        var page = await BodyAsync(technician, "/panel");
        Assert.DoesNotContain("Bu ay ciro", page);
        Assert.DoesNotContain("Geçen ay", page);
        Assert.Contains("Onay bekliyor", page); // sayıları teknisyen de görür
    }

    [DockerFact]
    public async Task Panel_requires_a_session()
    {
        Assert.StartsWith("/account/login", Browser.RedirectOf(await app.NewBrowser().GetAsync("/panel")));
    }
}
