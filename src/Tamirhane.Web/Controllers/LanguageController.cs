using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Tamirhane.Web.Localization;

namespace Tamirhane.Web.Controllers;

/// <summary>
/// Üst çubuktaki TR/EN seçici: seçilen dil bir yıllık çereze yazılır, kullanıcı geldiği sayfaya döner.
/// Dönüş adresi sadece bu sitenin içi olabilir (açık yönlendirme yok).
/// </summary>
[AllowAnonymous]
[Route("language")]
public sealed class LanguageController : Controller
{
    [HttpPost("")]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        var chosen = AppCultures.From(culture);
        if (chosen is not null)
        {
            Response.Cookies.Append(AppCultures.CookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(chosen)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = true, // __Host- öneki: sadece HTTPS, Path=/, alan adı yok
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    IsEssential = true, // kişisel veri değil, arayüz tercihi
                });
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
