namespace Tamirhane.Application.Abstractions;

/// <summary>Bir kullanım senaryosundaki bütün değişiklikleri tek seferde, tek transaction'da kaydeder.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
