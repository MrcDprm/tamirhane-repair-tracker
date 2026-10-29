using Tamirhane.Application.WorkOrders;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Application.Tests.WorkOrders;

/// <summary>Cihaz etiketi atölyede herkesin gözü önünde: müşterinin sadece adı ve soyadının baş harfi.</summary>
public class ReceiptViewTests
{
    private static ReceiptView With(string customerName) => new(
        "2026-0001", "CODE", new Uri("https://tamirhane.test/t/CODE"), DateTimeOffset.UtcNow,
        "Kaya Telefon", "Merkez", null, 90, "Ayşe Kaya",
        customerName, "+905321112233", null, DeviceKind.Phone, "Apple", "iPhone 13", null, null,
        "Ekran kırık", null, null, false, "tr");

    [Theory]
    [InlineData("Zeynep Arslan", "Zeynep A.")]
    [InlineData("Ayşe Nur Kaya", "Ayşe Nur K.")]
    [InlineData("Selin", "Selin")]
    [InlineData("  Şule   Öztürk ", "Şule Ö.")]
    public void Short_name_keeps_the_given_names_and_initial_of_the_surname(string name, string expected)
    {
        Assert.Equal(expected, With(name).CustomerShortName);
    }
}
