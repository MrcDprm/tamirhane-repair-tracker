using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Customers;

/// <summary>
/// E.164 biçiminde telefon numarası (+905321234567). Aynı numara farklı yazılsa da
/// ("0532 123 45 67", "+90 532 123 4567") tek bir değere döner; arama ve WhatsApp linki bunu kullanır.
/// </summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static Result<PhoneNumber> Parse(string? input)
    {
        var text = input?.Trim() ?? "";
        if (text.Length == 0 || text.Length > 30)
            return CustomerErrors.InvalidPhone;

        var international = text.StartsWith('+');
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        // Rakam, boşluk, tire, nokta ve parantez dışında karakter kabul edilmez.
        if (text.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '.' or '(' or ')' or '+')))
            return CustomerErrors.InvalidPhone;

        if (!international && digits.StartsWith("00"))
        {
            international = true;
            digits = digits[2..];
        }

        if (!international)
        {
            // Yurt içi yazım: 0532..., 532...
            if (digits.Length == 11 && digits[0] == '0')
                digits = digits[1..];
            if (digits.Length != 10)
                return CustomerErrors.InvalidPhone;
            digits = "90" + digits;
        }

        if (digits.StartsWith("90"))
        {
            // Türkiye: alan kodu 0 ya da 1 ile başlamaz, toplam 10 hane.
            var national = digits[2..];
            if (national.Length != 10 || national[0] is '0' or '1')
                return CustomerErrors.InvalidPhone;
        }
        else if (digits.Length is < 8 or > 15 || digits[0] == '0')
        {
            return CustomerErrors.InvalidPhone;
        }

        return new PhoneNumber("+" + digits);
    }

    public override string ToString() => Value;
}
