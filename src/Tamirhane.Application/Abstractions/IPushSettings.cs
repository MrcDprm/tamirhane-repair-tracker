namespace Tamirhane.Application.Abstractions;

/// <summary>
/// Tarayıcı bildirimi açık mı ve tarayıcıya verilecek VAPID açık anahtarı. Ayar yoksa PublicKey boştur:
/// takip sayfası düğmeyi göstermez, abonelik uçları 404 döner.
/// </summary>
public interface IPushSettings
{
    string? PublicKey { get; }
}
