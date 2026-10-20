using System.Globalization;
using System.Text;

namespace Tamirhane.Domain.Customers;

/// <summary>
/// Takip sayfası için isim maskeleme, kargo takip sayfalarındaki gibi: "Ayşe Kaya" → "Ay** Ka**".
/// Her isimde en fazla ilk 2 harf görünür ve her zaman en az bir harf gizli kalır ("Su" → "S*").
/// </summary>
public static class NameMask
{
    public static string Mask(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(MaskWord));
    }

    private static string MaskWord(string word)
    {
        // Harf sayısı karakter sayısı değil, ekranda görünen harf sayısıdır (birleşik harfler tek sayılır).
        var letters = new List<string>();
        var e = StringInfo.GetTextElementEnumerator(word);
        while (e.MoveNext())
            letters.Add(e.GetTextElement());

        var visible = Math.Min(2, letters.Count - 1);
        var result = new StringBuilder();
        for (var i = 0; i < letters.Count; i++)
            result.Append(i < visible ? letters[i] : "*");
        return result.ToString();
    }
}
