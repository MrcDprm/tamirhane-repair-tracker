namespace Tamirhane.Web.Models.Tracking;

/// <summary>Takip sayfasındaki teklif kararı. Ad kuralları domain'de (QuoteErrors.DeciderNameRequired).</summary>
public sealed class DecisionInput
{
    public bool Approved { get; set; }

    public string? DecidedBy { get; set; }
}
