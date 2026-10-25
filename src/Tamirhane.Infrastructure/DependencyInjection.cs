using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
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
            .AddPasswordValidator<PasswordPolicyValidator>();

        // /health ucu veritabanına bağlanılabiliyor mu diye bakar (yayında sunucunun sağlık kontrolü).
        services.AddHealthChecks().AddDbContextCheck<TamirhaneDbContext>("database");
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(ConnectionStrings.Normalize(connectionString))
            .UseSnakeCaseNamingConvention();
}
