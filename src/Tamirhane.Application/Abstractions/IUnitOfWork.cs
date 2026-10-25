namespace Tamirhane.Application.Abstractions;

/// <summary>Bir kullanım senaryosundaki bütün değişiklikleri tek seferde, tek transaction'da kaydeder.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Birden çok kaydetmeyi tek bütün yapar (ör. hesabı kendisi kaydeden Identity + atölye).
    /// CommitAsync çağrılmadan kapatılırsa hepsi geri alınır.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken ct);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
