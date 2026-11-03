using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.WebEncoders;
using Tamirhane.Application;
using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Notifications;
using Tamirhane.Infrastructure.Persistence;
using Tamirhane.Web.Authorization;
using Tamirhane.Web.Localization;
using Tamirhane.Web.ModelBinding;
using Tamirhane.Web.RateLimiting;
using Tamirhane.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// Geliştirmede repodaki .env okunur (ortam değişkenlerini her pencerede elle yüklemek gerekmesin). En düşük öncelikle
// eklenir: appsettings, gerçek ortam değişkenleri ve testlerin verdiği ayarlar her zaman ondan üstündür.
// Testler DOTENV=off verir; geliştiricinin .env'i test sonuçlarını değiştirmesin.
if (builder.Environment.IsDevelopment() && builder.Configuration["DOTENV"] != "off")
{
    var dotEnv = DotEnvFile.Read(builder.Environment.ContentRootPath);
    builder.Configuration.Sources.Insert(0, new MemoryConfigurationSource { InitialData = dotEnv! });
}

// Yayında bağlantı DATABASE_URL ortam değişkeninden gelir (Neon); geliştirmede appsettings.Development.json'dan.
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

// Arayüz metinleri Resources/tr.json ve en.json'dan (#35). Kendi fabrikamız AddLocalization'dan önce kaydedilir;
// AddLocalization sadece IStringLocalizer<T> sarmalayıcısını ekler.
builder.Services.AddSingleton(TextCatalog.Embedded);
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddLocalization();

builder.Services.AddControllersWithViews(options =>
{
    // Her POST, PUT ve DELETE CSRF token'ı taşımak zorunda; bir action'da [ValidateAntiForgeryToken] unutulsa da açık kalmaz.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // Ondalık alanlar sayfanın kültüründen bağımsız okunur (type="number" her dilde noktayla gönderir).
    options.ModelBinderProviders.Insert(0, new InvariantDecimalModelBinderProvider());
})
    .AddViewLocalization()
    // Doğrulama mesajları ve alan adları ([Required(ErrorMessage = "...")], [Display(Name = "...")]) aynı kaynaktan.
    .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));
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
// Tarayıcı bildirimi: üç değişken de boşsa kapalı; yarım ya da bozuk ayarda uygulama açılmaz.
builder.Services.AddWebPush(WebPushOptions.From(builder.Configuration["VAPID_PUBLIC_KEY"],
    builder.Configuration["VAPID_PRIVATE_KEY"], builder.Configuration["VAPID_SUBJECT"]));
builder.Services.AddGlobalRateLimit(builder.Configuration["CLIENT_IP_HEADER"]);
builder.Services.AddAccountRateLimit(builder.Configuration["CLIENT_IP_HEADER"]);
builder.Services.AddTrackingRateLimit(builder.Configuration["CLIENT_IP_HEADER"]);
builder.Services.AddDemoRateLimit(builder.Configuration["CLIENT_IP_HEADER"]);
builder.Services.AddShopPolicies();

// Kullanım senaryoları (her istek kendi örneğini alır) ve saat.
builder.Services.AddApplication();

// Yayında uygulamanın önünde Caddy var (Oracle sunucusu): TLS'i Caddy bitirir, uygulamaya aynı makineden http ile
// iletir. İstemcinin IP'si ve asıl şema (https) Caddy'nin yazdığı X-Forwarded-For / X-Forwarded-Proto'dan okunur.
// Bu başlıklara sadece makinenin kendisinden (127.0.0.1, ForwardedHeaders'ın varsayılan güvenilen vekili) gelen
// istekte inanılır; Caddy dışarıdan gelen X-Forwarded-For'u yok sayıp kendi gördüğü adresi yazar, uydurulamaz.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);

// Yayında Host başlığı sadece sitenin kendi adresleri olabilir (AllowedHosts ortam değişkeni); "*" ile açılmaz.
if (builder.Environment.IsProduction() && builder.Configuration["AllowedHosts"] is null or "*")
    throw new InvalidOperationException("AllowedHosts ortam değişkeni yok: sitenin alan adlarını ';' ile ayırarak verin.");

// HSTS bir yıl: tarayıcı siteye bir kez https ile girdikten sonra http'yi hiç denemez.
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
// Cevaplarda "Server: Kestrel" yazmasın: sunucu yazılımı dışarıya söylenmez.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var app = builder.Build();

// Yayında şema açılışta güncellenir (yeni migration'lar uygulanır); geliştirmede "dotnet ef database update".
if (app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<TamirhaneDbContext>().Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    // Yayında ayrıntı (stack trace) gösterilmez: genel hata sayfası ve istek numarası.
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
// Gövdesiz hata kodları (bulunamayan adres, bozuk form) çıplak tarayıcı sayfası yerine sitenin hata sayfasıyla.
app.UseStatusCodePagesWithReExecute("/error/{0}");

// Sağlık kontrolü sunucunun içinden, Caddy'siz http ile de sorulabilir; https'e yönlendirilmez.
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/healthz"), branch => branch.UseHttpsRedirection());
// Tarih ve sayı biçimi sunucunun işletim sistemine bağlı kalmasın (yayındaki Linux konteyneri kültürsüz çalışır):
// arayüz Türkçe ya da İngilizce (#35), kültürler AppCultures'ta.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(AppCultures.Turkish),
    SupportedCultures = [.. AppCultures.All],
    SupportedUICultures = [.. AppCultures.All],
    // Önce kişinin seçtiği dil (çerez), yoksa tarayıcının dili; ikisi de yoksa Türkçe.
    RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = AppCultures.CookieName }, new BrowserLanguageProvider()],
    ApplyCurrentCultureToResponseHeaders = true, // Content-Language
});
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
// /healthz: uygulama ayakta mı (izleme bunu sık sorar; veritabanına dokunmaz, Neon'u boşuna uyandırmaz).
// /health: veritabanına da ulaşılıyor mu (elle ya da izleme için).
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> uygulamayı bu sınıf üzerinden açar.
public partial class Program;
