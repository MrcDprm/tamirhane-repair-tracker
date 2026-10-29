using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Common;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Demo;

/// <summary>Demo atölyelerin kaydı. Oturumsuz çalışır: kiracı filtresi yerine her işlem atölye kimliğiyle.</summary>
public interface IDemoShops
{
    /// <summary>Verilen andan sonra açılmış (henüz süresi dolmamış) demo atölye sayısı.</summary>
    Task<int> CountOpenedSinceAsync(DateTimeOffset since, CancellationToken ct);

    /// <summary>Demo atölyenin bütün kayıtlarını ekler; kaydetme çağıranın işi (aynı transaction).</summary>
    void Add(DemoWorkshop workshop);
}

/// <summary>
/// "Demoyu dene" (#36): ziyaretçiye örnek verili, kendine özel bir atölye açılır ve sahibi olarak oturumu başlar.
/// Hesaplar doğrulanmış açılır ve adresleri ".invalid" (e-posta gitmez); şifre rastgele, kimse bilmez.
/// Aynı anda açık demo sayısı sınırlıdır (ücretsiz veritabanı dolmasın); IP başına sınır web katmanında.
/// </summary>
public sealed class StartDemo(IDemoShops demos, IAccountService accounts, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public const int MaxOpenDemos = 200;

    public async Task<Result> HandleAsync(string locale, CancellationToken ct)
    {
        var now = TurkeyTime.Now(clock);
        if (await demos.CountOpenedSinceAsync(now - Shop.DemoLifetime, ct) >= MaxOpenDemos)
            return ApplicationErrors.DemoFull;

        Guid ownerId;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(ct))
        {
            // Hesapları Identity kendisi kaydeder; atölye kaydı tutmazsa transaction ikisini de geri alır.
            var owner = await accounts.CreateConfirmedAsync(DemoData.Email("owner"), DemoData.Password(), ct);
            var technician = await accounts.CreateConfirmedAsync(DemoData.Email("tech"), DemoData.Password(), ct);
            if (owner.IsFailure || technician.IsFailure)
                throw new InvalidOperationException($"Demo hesabı açılamadı: {(owner.Error ?? technician.Error)!.Code}");

            demos.Add(DemoData.Build(Shop.Locales.Contains(locale) ? locale : "tr", owner.Value, technician.Value, now));
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            ownerId = owner.Value;
        }

        await accounts.SignInDemoAsync(ownerId, ct);
        return Result.Success();
    }
}
