using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.WebEncoders;
using Tamirhane.Application.Accounts;
using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Identity;
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
    // Her POST, PUT ve DELETE CSRF token'ı taşımak zorunda; bir action'da [ValidateAntiForgeryToken] unutulsa da açık kalmaz.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
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

// AddApplication() henüz çağrılamaz: bazı senaryoların bağımlılıkları (IShopSettings #18, INotificationQueue #19) yok,
// açılıştaki servis doğrulaması patlar. O zamana kadar ekranı olan senaryolar tek tek kaydedilir.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RegisterShop>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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
