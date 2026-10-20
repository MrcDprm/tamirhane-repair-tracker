namespace Tamirhane.Domain.WorkOrders;

/// <summary>
/// Ekranda ve fişte görünen iş emri numarası: yıl + atölye içinde o yılın sıra numarası (2026-0042).
/// Sadece okunabilirlik içindir; hiçbir erişim bu numarayla yapılmaz.
/// </summary>
public static class WorkOrderNumber
{
    public static string Format(int year, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        return $"{year}-{sequence:0000}";
    }
}
