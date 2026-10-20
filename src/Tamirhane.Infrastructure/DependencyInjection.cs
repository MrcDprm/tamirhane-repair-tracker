using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Persistence;

namespace Tamirhane.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TamirhaneDbContext>(options => Configure(options, connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TamirhaneDbContext>());
        services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IPartRepository, PartRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(ConnectionStrings.Normalize(connectionString))
            .UseSnakeCaseNamingConvention();
}
