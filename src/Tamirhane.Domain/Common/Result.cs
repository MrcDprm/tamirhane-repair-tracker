namespace Tamirhane.Domain.Common;

/// <summary>
/// Bir işlemin sonucu: ya başarılı ya da tek bir <see cref="Error"/>.
/// Beklenen hatalar (geçersiz geçiş, stok yetersiz) istisna fırlatmaz, böyle döner.
/// </summary>
public class Result
{
    private static readonly Result Ok = new(null);

    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => Ok;
    public static Result Failure(Error error) => new(error);
    public static Result<T> Success<T>(T value) => new(value, null);
    public static Result<T> Failure<T>(Error error) => new(default, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, Error? error) : base(error) => _value = value;

    /// <summary>Başarısız sonuçta okunursa programlama hatasıdır.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Başarısız sonucun değeri okunamaz: {Error!.Code}");

    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(Error error) => new(default, error);
}
