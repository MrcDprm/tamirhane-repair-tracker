using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Accounts;
using Tamirhane.Application.Customers;
using Tamirhane.Application.Inventory;
using Tamirhane.Application.Shops;
using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Application;

public static class DependencyInjection
{
    /// <summary>Kullanım senaryolarını kaydeder; her istek kendi örneğini alır.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterShop>();
        services.AddScoped<InviteStaff>();
        services.AddScoped<GetInvitation>();
        services.AddScoped<AcceptInvitation>();
        services.AddScoped<RevokeInvitation>();
        services.AddScoped<RemoveStaff>();
        services.AddScoped<ListStaff>();
        services.AddScoped<ListBranches>();
        services.AddScoped<AddBranch>();
        services.AddScoped<UpdateBranch>();
        services.AddScoped<SwitchBranch>();
        services.AddScoped<GetShopSettings>();
        services.AddScoped<UpdateShopSettings>();
        services.AddScoped<ReceiveDevice>();
        services.AddScoped<GetWorkOrder>();
        services.AddScoped<GetReceipt>();
        services.AddScoped<ListWorkOrders>();
        services.AddScoped<FindCustomerByPhone>();
        services.AddScoped<UpdateIntakeDetails>();
        services.AddScoped<PromiseDelivery>();
        services.AddScoped<GetTracking>();
        services.AddScoped<AddNote>();
        services.AddScoped<StartDiagnosis>();
        services.AddScoped<StartRepair>();
        services.AddScoped<AddQuoteLine>();
        services.AddScoped<RemoveQuoteLine>();
        services.AddScoped<SendQuote>();
        services.AddScoped<RecordQuoteDecision>();
        services.AddScoped<DecideQuoteByCustomer>();
        services.AddScoped<SubscribeToPush>();
        services.AddScoped<UnsubscribeFromPush>();
        services.AddScoped<GetPushState>();
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
        services.AddScoped<ListParts>();
        services.AddScoped<GetPart>();
        services.AddScoped<SetPartActive>();
        services.TryAddSingletonTimeProvider();
        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
