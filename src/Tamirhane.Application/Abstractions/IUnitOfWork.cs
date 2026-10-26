namespace Tamirhane.Application.Abstractions;

/// <summary>Bir kullanım senaryosundaki bütün değişiklikleri tek seferde, tek transaction'da kaydeder.</summary>
public interface IUnitOfWork
{
    /// <exception cref="ConcurrencyConflictException">Kayıt okunduktan sonra başka bir istek onu değiştirdi.</exception>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Birden çok kaydetmeyi tek bütün yapar (ör. hesabı kendisi kaydeden Identity + atölye).
    /// CommitAsync çağrılmadan kapatılırsa hepsi geri alınır.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken ct);
}

/// <summary>
/// İyimser kilit: aynı kaydı aynı anda iki istek değiştirmeye çalıştı (çift tıklama, iki sekme), ikincisi kaydedilmedi.
/// Kullanım senaryosu bunu "sayfa güncellendi, tekrar bak" türünden bir hataya çevirir.
/// </summary>
public sealed class ConcurrencyConflictException(Exception inner)
    : Exception("Kayıt okunduktan sonra başka bir istek tarafından değiştirildi.", inner);

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
