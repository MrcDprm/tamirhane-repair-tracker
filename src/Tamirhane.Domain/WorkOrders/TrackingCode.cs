using System.Security.Cryptography;

namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// Müşterinin takip linkindeki kod (/t/7K3M9QX2HFDA). 12 karakter Crockford base32 = 60 bit rastgelelik:
/// tahmin edilemez. Alfabede I, L, O, U yok; elle yazarken karışmasın diye O → 0, I/L → 1 sayılır.
/// </summary>
public static class TrackingCode
{
    public const int Length = 12;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate() => new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

    /// <summary>Kullanıcının yazdığı kodu arama için standart biçime çevirir; geçersizse boş döner.</summary>
    public static string? Normalize(string? input)
    {
        if (input is null)
            return null;

        Span<char> code = stackalloc char[Length];
        var count = 0;
        foreach (var raw in input)
        {
            if (raw is '-' or ' ')
                continue;
            var c = char.ToUpperInvariant(raw) switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                var other => other,
            };
            if (count == Length || !Alphabet.Contains(c))
                return null;
            code[count++] = c;
        }
        return count == Length ? new string(code) : null;
    }
}
