using Microsoft.AspNetCore.Identity;

namespace Tamirhane.Infrastructure.Identity;

/// <summary>
/// Giriş hesabı (e-posta, şifre özeti, kilit, güvenlik damgası). Sadece Infrastructure bilir;
/// atölyedeki rolü ve adı domain'deki Staff'ta, aradaki bağ Staff.UserId.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    // Diğer varlıklar gibi Guid v7: tahmin edilemez ama zamana göre sıralı.
    public ApplicationUser() => Id = Guid.CreateVersion7();
}
