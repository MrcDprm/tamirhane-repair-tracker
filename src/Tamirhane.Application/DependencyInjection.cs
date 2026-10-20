using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Customers;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Application;

public static class DependencyInjection
{
    /// <summary>Kullanım senaryolarını kaydeder; her istek kendi örneğini alır.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ReceiveDevice>();
        services.AddScoped<FindCustomerByPhone>();
        services.AddScoped<UpdateIntakeDetails>();
        services.AddScoped<AddNote>();
        services.AddScoped<StartDiagnosis>();
        services.AddScoped<StartRepair>();
        services.AddScoped<AddQuoteLine>();
        services.AddScoped<RemoveQuoteLine>();
        services.AddScoped<SendQuote>();
        services.AddScoped<RecordQuoteDecision>();
        services.AddScoped<DecideQuoteByCustomer>();
        services.AddScoped<MarkReady>();
        services.AddScoped<DeliverWorkOrder>();
        services.AddScoped<CancelWorkOrder>();
        services.AddScoped<RecordPayment>();
        services.AddScoped<RecordRefund>();
        services.AddScoped<SetDiagnosisFee>();
        services.AddScoped<ReopenUnderWarranty>();
        services.AddScoped<AddPart>();
        services.AddScoped<UpdatePart>();
        services.AddScoped<ChangeStock>();
        services.AddScoped<ListLowStock>();
        services.TryAddSingletonTimeProvider();
        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
