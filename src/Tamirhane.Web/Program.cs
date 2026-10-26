using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using Tamirhane.Application;
using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.ModelBinding;
using Tamirhane.Web.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Yayında bağlantı DATABASE_URL ortam değişkeninden gelir (Neon/Render); geliştirmede appsettings.Development.json'dan.
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("Tamirhane")
    ?? throw new InvalidOperationException(
        "Veritabanı bağlantısı yok: DATABASE_URL ortam değişkenini ya da ConnectionStrings:Tamirhane ayarını verin.");

// E-posta linklerinin başı. Geliştirmede launchSettings.json'daki https adresi: çerezler Secure olduğu için
// uygulama geliştirmede de https ile açılır (http'de CSRF kontrolü her formu reddeder).
var publicOrigin = AccountOptions.ParseOrigin(builder.Configuration["PUBLIC_ORIGIN"]
    ?? (builder.Environment.IsDevelopment() ? "https://localhost:7071" : null));

// Geliştirmede anahtar yoksa e-postalar konsola yazılır; yayında anahtarsız açılmak kayıtları sessizce bozardı.
var resendApiKey = builder.Configuration["RESEND_API_KEY"];
if (string.IsNullOrWhiteSpace(resendApiKey) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("RESEND_API_KEY ortam değişkeni yok; doğrulama e-postaları gönderilemez.");

builder.Services.AddControllersWithViews(options =>
{
    // Her POST, PUT ve DELETE CSRF token'ı taşımak zorunda; bir action'da [ValidateAntiForgeryToken] unutulsa da açık kalmaz.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Ondalık alanlar sayfanın kültüründen bağımsız okunur (type="number" her dilde noktayla gönderir).
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
});
// Türkçe harfler sayfaya "&#x15F;" gibi kodlanmadan yazılsın; <, >, &, " gibi HTML karakterleri yine kodlanır.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-tamirhane-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddAccounts(new AccountOptions(publicOrigin, resendApiKey,
    builder.Configuration["EMAIL_FROM"] ?? AccountOptions.DefaultFrom));
builder.Services.AddAccountRateLimit(builder.Configuration["CLIENT_IP_HEADER"]);
builder.Services.AddShopPolicies();

// Kullanım senaryoları (her istek kendi örneğini alır) ve saat.
builder.Services.AddApplication();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
// Tarih ve sayı biçimi sunucunun işletim sistemine bağlı kalmasın (yayındaki Linux konteyneri kültürsüz çalışır):
// arayüz Türkçe. İngilizce arayüz #35'te eklenir.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr-TR"),
    SupportedCultures = [new CultureInfo("tr-TR")],
    SupportedUICultures = [new CultureInfo("tr-TR")],
});
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> uygulamayı bu sınıf üzerinden açar.
public partial class Program;
