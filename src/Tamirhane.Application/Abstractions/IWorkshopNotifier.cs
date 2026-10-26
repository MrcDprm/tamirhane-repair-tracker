namespace Tamirhane.Application.Abstractions;

/// <summary>
/// Müşterinin takip sayfasından yaptığı işlemi atölyeye bildirir (sahiplere ve iş emrinin şubesindeki personele e-posta).
/// Gönderim arka planda; kayıt başarıyla yapıldıktan sonra çağrılır.
/// </summary>
public interface IWorkshopNotifier
{
    void QuoteDecided(Guid shopId, Guid workOrderId);
}
