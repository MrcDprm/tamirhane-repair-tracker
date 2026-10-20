namespace Tamirhane.Application.Common;

/// <summary>
/// Türkiye 2016'dan beri yıl boyu UTC+3. Sunucu UTC'de çalışsa da iş emri numarasının yılı
/// atölyenin takvimine göre belirlenir (31 Aralık gece 23:30'da açılan iş eski yıla yazılır).
/// </summary>
public static class TurkeyTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateTimeOffset Now(TimeProvider clock) => clock.GetUtcNow().ToOffset(Offset);
}
