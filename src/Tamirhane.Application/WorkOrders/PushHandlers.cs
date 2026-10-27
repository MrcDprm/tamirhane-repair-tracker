using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Application.WorkOrders;

/// <summary>Takip sayfasındaki "Durum değişince haber ver": tarayıcının verdiği abonelik bilgisi.</summary>
public sealed record PushSubscriptionInput(string? Endpoint, string? P256dh, string? Auth);

/// <summary>
/// Müşterinin tarayıcısı iş emrinin durum değişikliklerine abone olur. Giriş yoktur: erişim takip koduyla
/// (tahmin edilemez), bu yüzden ICurrentUser kullanılmaz. Adresin bir push servisi olup olmadığına domain karar verir.
/// </summary>
public sealed class SubscribeToPush(IWorkOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result> HandleAsync(string trackingCode, PushSubscriptionInput input, CancellationToken ct)
    {
        var code = TrackingCode.Normalize(trackingCode);
        var order = code is null ? null : await orders.GetByTrackingCodeAsync(code, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        var result = order.SubscribeToPush(input.Endpoint, input.P256dh, input.Auth, TurkeyTime.Now(clock));
        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync(ct);
        return result;
    }
}

/// <summary>Tarayıcı aboneliği bırakır ("Haber vermeyi durdur"). Abone değilse de başarılıdır.</summary>
public sealed class UnsubscribeFromPush(IWorkOrderRepository orders, IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(string trackingCode, string? endpoint, CancellationToken ct)
    {
        var code = TrackingCode.Normalize(trackingCode);
        var order = code is null ? null : await orders.GetByTrackingCodeAsync(code, ct);
        if (order is null)
            return ApplicationErrors.WorkOrderNotFound;

        order.UnsubscribeFromPush(endpoint);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Takip sayfası açılınca: bu tarayıcı (adresiyle) bu iş emrine abone mi? Düğme "haber ver" ya da "durdur" olur.
/// Aynı tarayıcı birden çok iş emrine abone olabildiği için tarayıcının kendi aboneliği bunu söyleyemez.
/// </summary>
public sealed class GetPushState(IWorkOrderRepository orders)
{
    public async Task<bool?> HandleAsync(string trackingCode, string? endpoint, CancellationToken ct)
    {
        var code = TrackingCode.Normalize(trackingCode);
        var order = code is null ? null : await orders.GetByTrackingCodeAsync(code, ct);
        if (order is null)
            return null;
        return order.PushSubscriptions.Any(s => s.Endpoint == endpoint?.Trim());
    }
}
