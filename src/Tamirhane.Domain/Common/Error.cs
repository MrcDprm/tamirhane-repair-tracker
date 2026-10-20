namespace Tamirhane.Domain.Common;

public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}

/// <summary>
/// Beklenen bir iş kuralı hatası. Code, arayüzde çevrilen metnin anahtarıdır
/// (ör. "work_order.invalid_transition"); kullanıcıya teknik ayrıntı gitmez.
/// </summary>
public sealed record Error(string Code, ErrorKind Kind)
{
    public static Error Validation(string code) => new(code, ErrorKind.Validation);
    public static Error NotFound(string code) => new(code, ErrorKind.NotFound);
    public static Error Conflict(string code) => new(code, ErrorKind.Conflict);
    public static Error Forbidden(string code) => new(code, ErrorKind.Forbidden);
}
