namespace Tamirhane.Domain.Customers;

/// <summary>IMEI: 15 hane, son hane Luhn kontrol hanesi. Yazımdaki boşluk ve tireler yok sayılır.</summary>
public static class Imei
{
    public static string? Normalize(string? input)
    {
        var digits = new string((input ?? "").Where(c => c is not (' ' or '-')).ToArray());
        return digits.Length == 15 && digits.All(char.IsAsciiDigit) && PassesLuhn(digits) ? digits : null;
    }

    /// <summary>Sağdan başlayarak her ikinci hane iki katına çıkar; toplam 10'a bölünmelidir.</summary>
    public static bool PassesLuhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
