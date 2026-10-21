using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure;
using Tamirhane.Web.Auth;

var builder = WebApplication.CreateBuilder(args);

// Yayında bağlantı DATABASE_URL ortam değişkeninden gelir (Neon/Render); geliştirmede appsettings.Development.json'dan.
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("Tamirhane")
    ?? throw new InvalidOperationException(
        "Veritabanı bağlantısı yok: DATABASE_URL ortam değişkenini ya da ConnectionStrings:Tamirhane ayarını verin.");

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<ICurrentUser, AnonymousUser>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

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
