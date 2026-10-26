using Tamirhane.Domain.Common;
using Tamirhane.Domain.Customers;

namespace Tamirhane.Domain.Shops;

/// <summary>
/// Atölyenin bir dükkânı. İş emirleri ve parça stoku şubeye bağlıdır. Her atölye kayıtta tek şubeyle açılır;
/// tek şubeli atölye şube kavramını arayüzde hiç görmez. Şube silinmez (iş emri ve stok geçmişi ona bağlı).
/// </summary>
public sealed class Branch : Entity
{
    public const int MaxNameLength = 100;
    public const int MaxAddressLength = 300;
    /// <summary>Bir atölyenin açabileceği en fazla şube (kötüye kullanıma karşı üst sınır).</summary>
    public const int MaxPerShop = 50;

    private Branch() { }

    public Guid ShopId { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Kabul fişinde ve takip sayfasında görünen adres; isteğe bağlı.</summary>
    public string? Address { get; private set; }
    /// <summary>Takip sayfasında müşterinin aradığı numara (E.164); isteğe bağlı.</summary>
    public string? Phone { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Kayıtta açılan ilk şubenin adı, atölyenin diline göre.</summary>
    public static string DefaultName(string locale) => locale == "en" ? "Main branch" : "Merkez";

    public static Result<Branch> Create(Guid shopId, string name, string? address, DateTimeOffset now, string? phone = null)
    {
        var branch = new Branch { ShopId = shopId, CreatedAt = now };
        var updated = branch.Update(name, address, phone);
        return updated.IsSuccess ? branch : updated.Error!;
    }

    public Result Update(string name, string? address, string? phone)
    {
        name = Collapse(name);
        address = Collapse(address);
        if (name.Length < 2 || name.Length > MaxNameLength)
            return ShopErrors.InvalidBranchName;
        if (address.Length > MaxAddressLength)
            return ShopErrors.InvalidBranchAddress;
        PhoneNumber? parsed = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var result = PhoneNumber.Parse(phone);
            if (result.IsFailure)
                return ShopErrors.InvalidBranchPhone;
            parsed = result.Value;
        }

        Name = name;
        Address = address.Length == 0 ? null : address;
        Phone = parsed?.Value;
        return Result.Success();
    }

    private static string Collapse(string? value) =>
        string.Join(' ', (value ?? "").Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
