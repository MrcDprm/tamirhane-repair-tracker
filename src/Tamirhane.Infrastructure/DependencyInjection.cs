using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TamirhaneDbContext>(options => Configure(options, connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TamirhaneDbContext>());
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IPartRepository, PartRepository>();
        services.AddScoped<IStockRepository, StockRepository>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                PasswordPolicy.Apply(options.Password);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true; // doğrulanmamış hesap atölyeye giremez
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<TamirhaneDbContext>()
            .AddPasswordValidator<PasswordPolicyValidator>()
            .AddDefaultTokenProviders();

        // Çerezler ve e-posta linklerindeki token'lar bu anahtarlarla imzalanır. Anahtarlar veritabanında durur:
        // sunucu yeniden başlayınca (her yayında) oturumlar kapanmaz, gönderilmiş linkler geçerli kalır.
        services.AddDataProtection()
            .SetApplicationName("Tamirhane")
            .PersistKeysToDbContext<TamirhaneDbContext>();

        // /health ucu veritabanına bağlanılabiliyor mu diye bakar (yayında sunucunun sağlık kontrolü).
        services.AddHealthChecks().AddDbContextCheck<TamirhaneDbContext>("database");
        return services;
    }

    /// <summary>Hesap servisi ve e-posta gönderimi.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, AccountOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IAccountService, AccountService>();

        if (string.IsNullOrWhiteSpace(options.ResendApiKey))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddHttpClient("resend", http =>
            {
                http.BaseAddress = new Uri("https://api.resend.com/");
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ResendApiKey);
                http.Timeout = TimeSpan.FromSeconds(15);
            });
            services.AddSingleton<IEmailSender>(sp => new ResendEmailSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("resend"), options.EmailFrom));
        }
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(ConnectionStrings.Normalize(connectionString))
            .UseSnakeCaseNamingConvention();
}
