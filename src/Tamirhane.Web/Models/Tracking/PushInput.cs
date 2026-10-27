namespace Tamirhane.Web.Models.Tracking;

/// <summary>
/// Tarayıcının PushSubscription.toJSON() çıktısı: { endpoint, keys: { p256dh, auth } }. Adres ve anahtar kuralları
/// domain'de (PushSubscription); burada sadece biçim.
/// </summary>
public sealed class PushInput
{
    public string? Endpoint { get; set; }

    public PushKeys? Keys { get; set; }
}

public sealed class PushKeys
{
    public string? P256dh { get; set; }

    public string? Auth { get; set; }
}
