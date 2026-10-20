using Tamirhane.Domain.WorkOrders;

namespace Tamirhane.Domain.Tests.WorkOrders;

public class TrackingCodeTests
{
    [Fact]
    public void Generated_codes_are_twelve_unambiguous_characters()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = TrackingCode.Generate();

            Assert.Equal(TrackingCode.Length, code.Length);
            Assert.DoesNotContain(code, c => c is 'I' or 'L' or 'O' or 'U' || char.IsLower(c));
            Assert.Equal(code, TrackingCode.Normalize(code));
        }
    }

    [Fact]
    public void Generated_codes_do_not_repeat()
    {
        var codes = Enumerable.Range(0, 10_000).Select(_ => TrackingCode.Generate()).ToHashSet();

        // 60 bit: 10.000 kodda çakışma olasılığı ~4·10⁻¹¹
        Assert.Equal(10_000, codes.Count);
    }

    [Theory]
    [InlineData("7k3m-9qx2-hfda", "7K3M9QX2HFDA")]
    [InlineData("7K3M 9QX2 HFDA", "7K3M9QX2HFDA")]
    [InlineData("oo1i-L000-0000", "001110000000")]
    public void Typed_codes_are_normalized(string input, string expected)
    {
        Assert.Equal(expected, TrackingCode.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7K3M9QX2HFD")]
    [InlineData("7K3M9QX2HFDAB")]
    [InlineData("7K3M9QX2HFDU")]
    [InlineData("../../etc/pas")]
    public void Invalid_codes_are_rejected(string? input)
    {
        Assert.Null(TrackingCode.Normalize(input));
    }

    [Theory]
    [InlineData(2026, 42, "2026-0042")]
    [InlineData(2026, 12345, "2026-12345")]
    public void Work_order_numbers_are_year_and_sequence(int year, int sequence, string expected)
    {
        Assert.Equal(expected, WorkOrderNumber.Format(year, sequence));
    }
}
