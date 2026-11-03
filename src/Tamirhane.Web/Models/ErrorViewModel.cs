namespace Tamirhane.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    /// <summary>Gövdesiz hata kodu (404, 400...); yakalanmayan hatada null.</summary>
    public int? StatusCode { get; set; }

    public bool NotFound => StatusCode == 404;

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId) && !NotFound;
}
