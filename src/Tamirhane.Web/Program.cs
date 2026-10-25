using Tamirhane.Infrastructure;
using Tamirhane.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Yayında bağlantı DATABASE_URL ortam değişkeninden gelir (Neon/Render); geliştirmede appsettings.Development.json'dan.
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("Tamirhane")
    ?? throw new InvalidOperationException(
        "Veritabanı bağlantısı yok: DATABASE_URL ortam değişkenini ya da ConnectionStrings:Tamirhane ayarını verin.");

// E-posta linklerinin başı. Geliştirmede launchSettings.json'daki http adresi.
var publicOrigin = AccountOptions.ParseOrigin(builder.Configuration["PUBLIC_ORIGIN"]
    ?? (builder.Environment.IsDevelopment() ? "http://localhost:5124" : null));

// Geliştirmede anahtar yoksa e-postalar konsola yazılır; yayında anahtarsız açılmak kayıtları sessizce bozardı.
var resendApiKey = builder.Configuration["RESEND_API_KEY"];
if (string.IsNullOrWhiteSpace(resendApiKey) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("RESEND_API_KEY ortam değişkeni yok; doğrulama e-postaları gönderilemez.");

builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-tamirhane-csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddAccounts(new AccountOptions(publicOrigin, resendApiKey,
    builder.Configuration["EMAIL_FROM"] ?? AccountOptions.DefaultFrom));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

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
