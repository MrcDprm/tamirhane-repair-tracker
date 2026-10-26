using Tamirhane.Application.WorkOrders;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>Hızlı arama: yazılanın ne olduğu (numara, telefon, IMEI, takip kodu, ad) doğru anlaşılıyor mu.</summary>
public class WorkOrderCriteriaTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_query_means_no_search(string? query)
    {
        Assert.Null(WorkOrderCriteria.Parse(query));
    }

    [Theory]
    [InlineData("2026-0042", "2026-0042")]
    [InlineData("2026-42", "2026-0042")]
    [InlineData("2026 - 0042", "2026-0042")]
    public void Full_work_order_number_is_found_exactly(string query, string number)
    {
        var c = WorkOrderCriteria.Parse(query)!;
        Assert.Equal(number, c.Number);
        Assert.Null(c.Text);
    }

    [Fact]
    public void Short_number_matches_the_end_of_the_work_order_number()
    {
        var c = WorkOrderCriteria.Parse("42")!;
        Assert.Equal("0042", c.NumberSuffix);
        Assert.Null(c.Text);
        Assert.Null(c.Phone);
    }

    [Theory]
    [InlineData("0532 123 45 67")]
    [InlineData("+90 532 123 4567")]
    [InlineData("5321234567")]
    public void Phone_in_any_writing_is_found(string query)
    {
        Assert.Equal("+905321234567", WorkOrderCriteria.Parse(query)!.Phone!.Value);
    }

    [Fact]
    public void Valid_imei_is_recognised_and_invalid_one_is_not()
    {
        Assert.Equal("490154203237518", WorkOrderCriteria.Parse("49015 4203 237518")!.Imei);
        Assert.Null(WorkOrderCriteria.Parse("490154203237519")!.Imei); // Luhn tutmuyor
    }

    [Fact]
    public void Tracking_code_is_recognised_with_easy_to_confuse_letters()
    {
        Assert.Equal("7K3M9QX2HFDA", WorkOrderCriteria.Parse("7k3m-9qx2-hfda")!.TrackingCode);
    }

    [Theory]
    [InlineData("Zeynep")]
    [InlineData("iphone 13")]
    [InlineData("%_")]
    public void Anything_else_is_free_text(string query)
    {
        var c = WorkOrderCriteria.Parse(query)!;
        Assert.Equal(query, c.Text);
        Assert.Null(c.Number);
        Assert.Null(c.NumberSuffix);
    }

    [Fact]
    public void Very_long_query_is_cut()
    {
        Assert.Equal(WorkOrderCriteria.MaxQueryLength, WorkOrderCriteria.Parse(new string('a', 500))!.Text!.Length);
    }
}
