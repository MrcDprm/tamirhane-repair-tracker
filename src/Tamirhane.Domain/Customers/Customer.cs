using System.Net.Mail;
using Tamirhane.Domain.Common;

namespace Tamirhane.Domain.Customers;

/// <summary>Atölyenin müşterisi. Telefon numarası atölye içinde tekildir (arama bununla yapılır).</summary>
public sealed class Customer : Entity
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;

    private Customer() { }

    public Guid ShopId { get; private set; }
    public string Name { get; private set; } = "";
    public PhoneNumber Phone { get; private set; } = null!;
    public string? Email { get; private set; }
    /// <summary>KVKK aydınlatma metninin müşteriye gösterilip onaylandığı an.</summary>
    public DateTimeOffset KvkkConsentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Customer> Create(Guid shopId, string name, string phone, string? email, bool kvkkConsent, DateTimeOffset now)
    {
        if (!kvkkConsent)
            return CustomerErrors.ConsentRequired;

        var customer = new Customer { ShopId = shopId, KvkkConsentAt = now, CreatedAt = now };
        var result = customer.Update(name, phone, email);
        return result.IsSuccess ? customer : result.Error!;
    }

    public Result Update(string name, string phone, string? email)
    {
        name = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2 || name.Length > MaxNameLength)
            return CustomerErrors.InvalidName;

        var parsedPhone = PhoneNumber.Parse(phone);
        if (parsedPhone.IsFailure)
            return parsedPhone.Error!;

        var parsedEmail = NormalizeEmail(email);
        if (parsedEmail.IsFailure)
            return parsedEmail.Error!;

        Name = name;
        Phone = parsedPhone.Value;
        Email = parsedEmail.Value;
        return Result.Success();
    }

    private static Result<string?> NormalizeEmail(string? email)
    {
        email = email?.Trim();
        if (string.IsNullOrEmpty(email))
            return Result.Success<string?>(null);
        // MailAddress, "Ad <adres>" gibi biçimleri de kabul eder; sadece çıplak adres istiyoruz.
        if (email.Length > MaxEmailLength || email.Contains(' ')
            || !MailAddress.TryCreate(email, out var address) || address.Address != email || !address.Host.Contains('.'))
            return CustomerErrors.InvalidEmail;

        return Result.Success<string?>(email.ToLowerInvariant());
    }
}
