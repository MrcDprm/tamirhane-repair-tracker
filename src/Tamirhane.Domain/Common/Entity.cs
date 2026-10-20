namespace Tamirhane.Domain.Common;

/// <summary>
/// Kimliği olan bütün varlıkların tabanı. Kimlik Guid v7: tahmin edilemez ama
/// zamana göre sıralı, bu yüzden veritabanı indeksini dağıtmaz.
/// </summary>
public abstract class Entity
{
    protected Entity() => Id = Guid.CreateVersion7();

    public Guid Id { get; private set; }
}
