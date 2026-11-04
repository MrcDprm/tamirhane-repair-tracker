using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Tamirhane.Web.Tests.ShopFlows;

namespace Tamirhane.Web.Tests;

/// <summary>
/// Yapay zekâ asistanı bağlantısı uçtan uca: dinamik kayıt → giriş + izin ekranı → kod + PKCE → token → MCP araçları.
/// Güvenlik: token'sız ve sadece çerezle istek reddedilir, bir atölyenin token'ı başka atölyeyi görmez, iptal hemen etkili.
/// </summary>
[Collection(WebCollection.Name)]
public sealed partial class McpFlowTests(TamirhaneWebFactory app)
{
    private const string Callback = "http://127.0.0.1:33418/callback";
    /// <summary>Testte PUBLIC_ORIGIN yok; geliştirme varsayılanı https://localhost:7071 (Program.cs).</summary>
    private const string Resource = "https://localhost:7071/mcp";
    private readonly ShopFlows _flows = new(app);

    [DockerFact]
    public async Task Mcp_without_token_returns_401_pointing_to_resource_metadata()
    {
        var client = app.NewBrowser();
        var response = await client.SendAsync(McpRequest(null, "tools/list", new { }));

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{(int)response.StatusCode} {response.Headers.Location} {await response.Content.ReadAsStringAsync()}");
        var challenge = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=", challenge);

        var metadataUrl = ResourceMetadataUrl().Match(challenge).Groups[1].Value;
        using var metadata = JsonDocument.Parse(await (await client.GetAsync(new Uri(metadataUrl).PathAndQuery)).Content.ReadAsStringAsync());
        Assert.EndsWith("/mcp", metadata.RootElement.GetProperty("resource").GetString());
        Assert.NotEmpty(metadata.RootElement.GetProperty("authorization_servers").EnumerateArray());
    }

    [DockerFact]
    public async Task Authorization_server_metadata_advertises_registration_and_only_s256()
    {
        var response = await app.NewBrowser().GetAsync("/.well-known/oauth-authorization-server");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.EndsWith("/connect/register", metadata.RootElement.GetProperty("registration_endpoint").GetString());
        var methods = metadata.RootElement.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(["S256"], methods);
    }

    [DockerFact]
    public async Task Registration_refuses_redirect_addresses_outside_the_allowlist()
    {
        foreach (var bad in new[] { "https://kotu.example/callback", "http://claude.ai/api/mcp/auth_callback", "javascript:alert(1)" })
        {
            var response = await app.NewBrowser().PostAsync("/connect/register",
                JsonContent.Create(new Dictionary<string, object> { ["redirect_uris"] = new[] { bad }, ["token_endpoint_auth_method"] = "none" }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid_redirect_uri", await response.Content.ReadAsStringAsync());
        }
    }

    [DockerFact]
    public async Task Owner_connects_and_uses_the_tools()
    {
        var owner = await _flows.OwnerAsync("mcp-sahip@example.com");
        await ReceiveAsync(owner, "0532 701 01 01");
        var token = await ConnectAsync(owner, "Claude");

        using var tools = await CallAsync(token, "tools/list", new { });
        var names = tools.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { "find_work_order", "list_work_orders", "low_stock", "change_status" }, names);

        var found = await ToolAsync(token, "find_work_order", new { query = "0532 701 01 01" });
        var match = found.GetProperty("matches")[0];
        Assert.Equal("Received", match.GetProperty("status").GetString());
        Assert.Equal("Zeynep Arslan", match.GetProperty("customer").GetProperty("name").GetString());
        Assert.Equal("Ekran kırık", match.GetProperty("customerReported").GetProperty("complaint").GetString());
        Assert.False(match.TryGetProperty("passcode", out _)); // cihaz şifresi asistana hiç gitmez
        var number = match.GetProperty("number").GetString()!;

        var changed = await ToolAsync(token, "change_status", new { number, action = "start_diagnosis", note = "Asistandan" });
        Assert.Equal("Diagnosing", changed.GetProperty("status").GetString());

        // Geçersiz geçiş domain'de reddedilir, anlamlı mesajla.
        using var invalid = await CallAsync(token, "tools/call", new { name = "change_status", arguments = new { number, action = "deliver" } });
        Assert.True(invalid.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());

        var list = await ToolAsync(token, "list_work_orders", new { state = "open" });
        Assert.Contains(list.GetProperty("workOrders").EnumerateArray(), r => r.GetProperty("number").GetString() == number);
    }

    [DockerFact]
    public async Task Token_of_one_shop_cannot_see_or_change_another_shops_work_orders()
    {
        var first = await _flows.OwnerAsync("mcp-birinci@example.com");
        await ReceiveAsync(first, "0532 702 02 02");
        var firstNumber = (await ToolAsync(await ConnectAsync(first, "Claude"), "find_work_order", new { query = "0532 702 02 02" }))
            .GetProperty("matches")[0].GetProperty("number").GetString()!;

        var other = await ConnectAsync(await _flows.OwnerAsync("mcp-ikinci@example.com"), "Claude");
        var found = await ToolAsync(other, "find_work_order", new { query = "0532 702 02 02" });
        Assert.Empty(found.GetProperty("matches").EnumerateArray());

        using var change = await CallAsync(other, "tools/call", new { name = "change_status", arguments = new { number = firstNumber, action = "start_diagnosis" } });
        Assert.True(change.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    [DockerFact]
    public async Task Session_cookie_alone_is_not_accepted_on_mcp()
    {
        var owner = await _flows.OwnerAsync("mcp-cerez@example.com");
        var response = await owner.SendAsync(McpRequest(null, "tools/list", new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [DockerFact]
    public async Task Disconnecting_on_the_connections_page_revokes_the_token_immediately()
    {
        var owner = await _flows.OwnerAsync("mcp-iptal@example.com");
        var token = await ConnectAsync(owner, "Claude Test");
        Assert.Equal(HttpStatusCode.OK, (await app.NewBrowser().SendAsync(McpRequest(token, "tools/list", new { }))).StatusCode);

        var page = await BodyAsync(owner, "/connections");
        Assert.Contains("Claude Test", page);
        var revoke = RevokeAction().Match(page).Groups[1].Value;
        Assert.Equal("/connections", Browser.RedirectOf(await owner.SubmitAsync("/connections", revoke, [])));

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.NewBrowser().SendAsync(McpRequest(token, "tools/list", new { }))).StatusCode);
    }

    [DockerFact]
    public async Task Denying_on_the_consent_screen_returns_access_denied_to_the_client()
    {
        var owner = await _flows.OwnerAsync("mcp-ret@example.com");
        var (clientId, _) = await RegisterAsync("Claude");
        var (authorize, _) = AuthorizeUrl(clientId);

        var response = await owner.SubmitAsync(authorize, "/connect/authorize", Fields(authorize, "deny"));
        Assert.StartsWith(Callback, response.Headers.Location!.AbsoluteUri);
        Assert.Contains("error=access_denied", response.Headers.Location!.Query);
    }

    [DockerFact]
    public async Task Signed_out_user_is_sent_to_login_and_back_to_the_consent_screen()
    {
        var (clientId, _) = await RegisterAsync("Claude");
        var (authorize, _) = AuthorizeUrl(clientId);
        var response = await app.NewBrowser().GetAsync(authorize);
        Assert.True(response.StatusCode == HttpStatusCode.Found, $"{(int)response.StatusCode} {response.Headers.Location} {await response.Content.ReadAsStringAsync()}");
        Assert.StartsWith("/account/login", Browser.RedirectOf(response));
    }

    // --- yardımcılar ---

    /// <summary>İstemci kaydı + izin ekranında onay + kodu token'a çevirme; erişim token'ını döner.</summary>
    private async Task<string> ConnectAsync(Browser user, string appName)
    {
        var (clientId, _) = await RegisterAsync(appName);
        var (authorize, verifier) = AuthorizeUrl(clientId);

        var consent = await BodyAsync(user, authorize);
        Assert.Contains(WebUtility.HtmlEncode(appName), consent);

        var allowed = await user.SubmitAsync(authorize, "/connect/authorize", Fields(authorize, "allow"));
        Assert.True(allowed.Headers.Location is not null, $"{(int)allowed.StatusCode} {await allowed.Content.ReadAsStringAsync()}");
        var location = allowed.Headers.Location!;
        Assert.StartsWith(Callback, location.AbsoluteUri);
        var code = QueryValue(location.Query, "code");

        var token = await app.NewBrowser().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = Callback,
            ["client_id"] = clientId, ["code_verifier"] = verifier, ["resource"] = Resource,
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        using var json = JsonDocument.Parse(await token.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<(string ClientId, string? Secret)> RegisterAsync(string name)
    {
        var response = await app.NewBrowser().PostAsync("/connect/register", JsonContent.Create(new Dictionary<string, object>
        {
            ["client_name"] = name, ["redirect_uris"] = new[] { Callback }, ["token_endpoint_auth_method"] = "none",
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
        }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("client_id").GetString()!, null);
    }

    private static (string Url, string Verifier) AuthorizeUrl(string clientId)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId, ["redirect_uri"] = Callback, ["scope"] = "mcp",
            ["state"] = "durum-123", ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["resource"] = Resource, // claude.ai gibi istemciler RFC 8707 kaynak göstergesini gönderir
        };
        return ("/connect/authorize?" + string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}")), verifier);
    }

    private static Dictionary<string, string> Fields(string authorizeUrl, string decision)
    {
        var fields = new Uri("https://localhost" + authorizeUrl).Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        fields["decision"] = decision;
        return fields;
    }

    private static HttpRequestMessage McpRequest(string? token, string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters }),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<JsonDocument> CallAsync(string token, string method, object parameters)
    {
        var response = await app.NewBrowser().SendAsync(McpRequest(token, method, parameters));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Cevap düz JSON ya da SSE ("data: {...}") olabilir.
        var json = body.TrimStart().StartsWith('{')
            ? body
            : body.Split('\n').Select(l => l.Trim()).Last(l => l.StartsWith("data:", StringComparison.Ordinal))["data:".Length..];
        return JsonDocument.Parse(json);
    }

    private async Task<JsonElement> ToolAsync(string token, string name, object arguments)
    {
        using var response = await CallAsync(token, "tools/call", new { name, arguments });
        var result = response.RootElement.GetProperty("result");
        Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(), result.ToString());
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static string QueryValue(string query, string name) =>
        query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p[0] == name).Select(p => Uri.UnescapeDataString(p[1])).Single();

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("resource_metadata=\"([^\"]+)\"")]
    private static partial Regex ResourceMetadataUrl();

    [GeneratedRegex("action=\"(/connections/[^\"]+/revoke)\"")]
    private static partial Regex RevokeAction();
}
