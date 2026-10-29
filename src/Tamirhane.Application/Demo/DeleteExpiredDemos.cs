using Tamirhane.Application.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Demo;

/// <summary>
/// Süresi dolan demo atölyeleri (#36), bütün kayıtları ve hesaplarıyla siler. Arka planda düzenli çalışır;
/// her atölye kendi transaction'ında silinir: biri hata verirse diğerleri yine silinir, yarım kalan atölye olmaz.
/// </summary>
public sealed class DeleteExpiredDemos(IDemoShops demos, TimeProvider clock)
{
    public const int BatchSize = 50;

    /// <returns>Silinen demo atölye sayısı.</returns>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var openedBefore = TurkeyTime.Now(clock) - Shop.DemoLifetime;
        var deleted = 0;
        while (true)
        {
            var expired = await demos.ListOpenedBeforeAsync(openedBefore, BatchSize, ct);
            foreach (var shopId in expired)
            {
                await demos.DeleteAsync(shopId, ct);
                deleted++;
            }
            if (expired.Count < BatchSize)
                return deleted;
        }
    }
}
