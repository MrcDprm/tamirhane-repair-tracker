using System.Net;
using System.Text.RegularExpressions;

namespace Tamirhane.Web.Tests;

/// <summary>Testte bir kullanıcının tarayıcısı: çerezleri saklar, formları sayfadaki CSRF token'ıyla gönderir.</summary>
public sealed partial class Browser(HttpClient client)
{
    public Task<HttpResponseMessage> GetAsync(string url) => client.GetAsync(url);

    /// <summary>Sayfadaki JavaScript'in attığı gibi bir istek (ör. JSON gövde, CSRF başlığı); çerezler aynı.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => client.SendAsync(request);

    public Task<HttpResponseMessage> PostAsync(string url, HttpContent content) => client.PostAsync(url, content);

    /// <summary>
    /// Formun bulunduğu sayfayı açar, oradaki CSRF token'ını alır ve alanları gönderir; gerçek bir form gönderimi gibi.
    /// </summary>
    public async Task<HttpResponseMessage> SubmitAsync(
        string page, string action, Dictionary<string, string> fields, Dictionary<string, string>? headers = null)
    {
        var html = await client.GetStringAsync(page);
        var token = AntiforgeryField().Match(html);
        Assert.True(token.Success, $"{page} sayfasında CSRF token'ı yok.");

        fields["__RequestVerificationToken"] = token.Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(fields) };
        foreach (var (name, value) in headers ?? [])
            request.Headers.Add(name, value); // ör. htmx'in gönderdiği HX-Request
        return await client.SendAsync(request);
    }

    /// <summary>Yönlendirilen adres; yönlendirme yoksa test düşer.</summary>
    public static string RedirectOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryField();
}
