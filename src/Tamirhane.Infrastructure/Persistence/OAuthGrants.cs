using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Tamirhane.Infrastructure.Persistence;

/// <summary>
/// Hesap ya da atölye silinince o hesapların yapay zekâ bağlantıları (izinler ve token'lar) da silinir. OpenIddict'in
/// tablolarında hesaba yabancı anahtar yok (konu "subject" metin olarak durur), zincirleme silme kendiliğinden olmaz.
/// </summary>
internal static class OAuthGrants
{
    public static async Task DeleteForUsersAsync(TamirhaneDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return;
        var subjects = userIds.Select(id => id.ToString()).ToList();
        await db.Set<OpenIddictEntityFrameworkCoreToken>().Where(t => t.Subject != null && subjects.Contains(t.Subject)).ExecuteDeleteAsync(ct);
        await db.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .Where(a => a.Subject != null && subjects.Contains(a.Subject)).ExecuteDeleteAsync(ct);
    }
}
