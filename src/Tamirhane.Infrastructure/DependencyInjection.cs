using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Background;
using Tamirhane.Infrastructure.Email;
using Tamirhane.Infrastructure.Identity;
using Tamirhane.Infrastructure.Notifications;
using Tamirhane.Infrastructure.Persistence;
using Tamirhane.Infrastructure.Printing;

namespace Tamirhane.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TamirhaneDbContext>(options => Configure(options, connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TamirhaneDbContext>());
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IShopSettings, ShopSettingsReader>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<ITrackingDirectory, TrackingDirectory>();
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
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddEntityFrameworkStores<TamirhaneDbContext>()
            .AddPasswordValidator<PasswordPolicyValidator>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        // Çerezler ve e-posta linklerindeki token'lar bu anahtarlarla imzalanır. Anahtarlar veritabanında durur:
        // sunucu yeniden başlayınca (her yayında) oturumlar kapanmaz, gönderilmiş linkler geçerli kalır.
        services.AddDataProtection()
            .SetApplicationName("Tamirhane")
            .PersistKeysToDbContext<TamirhaneDbContext>();

        services.AddBackgroundJobs();

        // /health ucu veritabanına bağlanılabiliyor mu diye bakar (yayında sunucunun sağlık kontrolü).
        services.AddHealthChecks().AddDbContextCheck<TamirhaneDbContext>("database");
        return services;
    }

    /// <summary>
    /// Arka plan işleri (e-posta, iş emri bildirimleri): istek işi ekler ve döner, tek bir işçi sırayla çalıştırır.
    /// Seçenekler önceden kaydedildiyse (testte sıfır bekleme) onlar kullanılır.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton(BackgroundJobOptions.Default);
        services.AddHostedService<BackgroundJobWorker>();
        services.AddSingleton<EmailOutbox>();
        services.AddScoped<INotificationQueue, WorkOrderNotificationQueue>();
        services.AddScoped<IWorkshopNotifier, WorkshopNotifier>();
        services.AddScoped<WorkshopEmails>();
        return services;
    }

    /// <summary>Oturum (çerez), oturumdaki personel, hesap servisi ve e-posta gönderimi.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, AccountOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ITrackingLinks>(new TrackingLinks(options.PublicOrigin));
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        new IdentityBuilder(typeof(ApplicationUser), services)
            .AddSignInManager()
            .AddClaimsPrincipalFactory<ShopClaimsPrincipalFactory>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(cookie =>
        {
            // __Host- öneki tarayıcıya şunu zorunlu kılar: sadece HTTPS, Domain yok, Path=/; alt alan adı çerezi ezemez.
            cookie.Cookie.Name = "__Host-tamirhane";
            cookie.Cookie.HttpOnly = true; // JavaScript okuyamaz (XSS olsa bile oturum çalınamaz)
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Lax; // başka siteden gelen POST'ta çerez gitmez
            cookie.Cookie.Path = "/";
            cookie.LoginPath = "/account/login";
            cookie.LogoutPath = "/account/logout";
            cookie.AccessDeniedPath = "/account/access-denied";
            cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
            cookie.SlidingExpiration = true;
        });
        // Güvenlik damgası her istekte kontrol edilir: şifre değişince ya da oturumlar kapatılınca diğer cihazlar hemen düşer.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

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
            var from = EmailFrom.Parse(options.EmailFrom); // bozuksa uygulama açılırken durur
            services.AddSingleton<IEmailSender>(sp => new ResendEmailSender(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("resend"), from));
        }
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(ConnectionStrings.Normalize(connectionString))
            .UseSnakeCaseNamingConvention();
}
