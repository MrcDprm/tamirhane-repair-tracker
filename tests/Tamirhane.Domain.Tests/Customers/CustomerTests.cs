using Tamirhane.Domain.Customers;

namespace Tamirhane.Domain.Tests.Customers;

public class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.FromHours(3));

    [Theory]
    [InlineData("0532 123 45 67", "+905321234567")]
    [InlineData("05321234567", "+905321234567")]
    [InlineData("532 123 4567", "+905321234567")]
    [InlineData("+90 532 123 45 67", "+905321234567")]
    [InlineData("0090 532 123 45 67", "+905321234567")]
    [InlineData("(0212) 444-12-34", "+902124441234")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("+1 (415) 555-2671", "+14155552671")]
    public void Phone_numbers_are_normalized_to_e164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Parse(input).Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0032 123 45 67 89 01 23 45")]
    [InlineData("0132 123 45 67")]
    [InlineData("+90 532 123 45")]
    [InlineData("0532 123 45 67 ext 5")]
    [InlineData("+0 123 456 789")]
    public void Invalid_phone_numbers_are_rejected(string input)
    {
        Assert.Equal(CustomerErrors.InvalidPhone, PhoneNumber.Parse(input).Error);
    }

    [Fact]
    public void Customer_requires_kvkk_consent()
    {
        var result = Customer.Create(Guid.NewGuid(), "Ayşe Kaya", "0532 123 45 67", null, kvkkConsent: false, Now);

        Assert.Equal(CustomerErrors.ConsentRequired, result.Error);
    }

    [Fact]
    public void Customer_normalizes_name_phone_and_email()
    {
        var customer = Customer.Create(Guid.NewGuid(), "  Ayşe   Kaya ", "0532 123 45 67", " Ayse.Kaya@Example.com ", true, Now).Value;

        Assert.Equal("Ayşe Kaya", customer.Name);
        Assert.Equal("+905321234567", customer.Phone.Value);
        Assert.Equal("ayse.kaya@example.com", customer.Email);
        Assert.Equal(Now, customer.KvkkConsentAt);
    }

    [Theory]
    [InlineData("ayse")]
    [InlineData("ayse@")]
    [InlineData("ayse@localhost")]
    [InlineData("Ayşe <ayse@example.com>")]
    [InlineData("ayse kaya@example.com")]
    public void Invalid_emails_are_rejected(string email)
    {
        var result = Customer.Create(Guid.NewGuid(), "Ayşe Kaya", "0532 123 45 67", email, true, Now);

        Assert.Equal(CustomerErrors.InvalidEmail, result.Error);
    }

    [Fact]
    public void Email_is_optional_and_name_needs_two_letters()
    {
        Assert.Null(Customer.Create(Guid.NewGuid(), "Ayşe Kaya", "0532 123 45 67", "  ", true, Now).Value.Email);
        Assert.Equal(CustomerErrors.InvalidName, Customer.Create(Guid.NewGuid(), "A", "0532 123 45 67", null, true, Now).Error);
    }

    [Theory]
    [InlineData("490154203237518", true)]
    [InlineData("49-015420-323751-8", true)]
    [InlineData("490154203237517", false)]
    [InlineData("49015420323751", false)]
    [InlineData("49015420323751A", false)]
    public void Imei_needs_fifteen_digits_and_a_valid_check_digit(string input, bool valid)
    {
        Assert.Equal(valid, Imei.Normalize(input) is not null);
    }

    [Fact]
    public void Device_belongs_to_the_customer_and_validates_imei()
    {
        var owner = Customer.Create(Guid.NewGuid(), "Ayşe Kaya", "0532 123 45 67", null, true, Now).Value;

        var device = Device.Create(owner, DeviceKind.Phone, " Apple ", " iPhone 13 ", " f2lx123 ", "49 0154 2032 3751 8").Value;

        Assert.Equal(owner.Id, device.CustomerId);
        Assert.Equal(owner.ShopId, device.ShopId);
        Assert.Equal("Apple", device.Brand);
        Assert.Equal("F2LX123", device.Serial);
        Assert.Equal("490154203237518", device.Imei);
        Assert.Equal(CustomerErrors.InvalidImei, Device.Create(owner, DeviceKind.Phone, "Apple", "iPhone 13", null, "123").Error);
        Assert.Equal(CustomerErrors.InvalidBrand, Device.Create(owner, DeviceKind.Phone, " ", "iPhone 13", null, null).Error);
        Assert.Equal(CustomerErrors.InvalidKind, Device.Create(owner, (DeviceKind)99, "Apple", "iPhone 13", null, null).Error);
    }

    [Theory]
    [InlineData("Ayşe Kaya", "Ay** Ka**")]
    [InlineData("Ali", "Al*")]
    [InlineData("Su", "S*")]
    [InlineData("A", "*")]
    [InlineData("  Mehmet   Ali  Öztürk ", "Me**** Al* Öz****")]
    [InlineData("", "")]
    public void Names_are_masked_like_a_parcel_tracking_page(string name, string expected)
    {
        Assert.Equal(expected, NameMask.Mask(name));
    }
}
