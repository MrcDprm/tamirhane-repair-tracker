using Tamirhane.Application.Abstractions;
using Tamirhane.Application.Accounts;
using Tamirhane.Domain.Common;
using Tamirhane.Domain.Shops;

namespace Tamirhane.Application.Tests.Fakes;

/// <summary>Bellek içi hesaplar; gönderilen e-postaları da kaydeder.</summary>
public sealed class FakeAccounts : IAccountService
{
    public Dictionary<string, Guid> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(Guid UserId, string Locale)> ConfirmationsSent { get; } = [];
    public List<(string Email, string Locale)> AlreadyRegisteredSent { get; } = [];
    public Error? NextPasswordError { get; set; }

    public Task<Result<Guid>> CreateAsync(string email, string password, CancellationToken ct)
    {
        if (Users.ContainsKey(email))
            return Task.FromResult<Result<Guid>>(AccountErrors.EmailTaken);
        if (NextPasswordError is { } error)
            return Task.FromResult<Result<Guid>>(error);

        var id = Guid.NewGuid();
        Users[email] = id;
        return Task.FromResult<Result<Guid>>(id);
    }

    public Task SendEmailConfirmationAsync(Guid userId, string locale, CancellationToken ct)
    {
        ConfirmationsSent.Add((userId, locale));
        return Task.CompletedTask;
    }

    public Task SendAlreadyRegisteredAsync(string email, string locale, CancellationToken ct)
    {
        AlreadyRegisteredSent.Add((email, locale));
        return Task.CompletedTask;
    }

    // Oturum işleri kullanım senaryolarında yok; gerçek Identity ile Web testlerinde denenir.
    public Task<SignInOutcome> SignInAsync(string email, string password, bool remember, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task SignOutAsync() => throw new NotSupportedException();

    public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task ResendEmailConfirmationAsync(string email, CancellationToken ct) => throw new NotSupportedException();
}

public sealed class FakeShops : IShopRepository
{
    public List<(Shop Shop, Staff Owner)> Items { get; } = [];

    public void Add(Shop shop, Staff owner) => Items.Add((shop, owner));
}
