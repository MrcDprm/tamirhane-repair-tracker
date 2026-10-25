using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tamirhane.Application.Abstractions;
using Tamirhane.Infrastructure.Identity;

namespace Tamirhane.Infrastructure.Tests.Identity;

/// <summary>
/// Uygulamanın kayıtlı şifre kurallarını (Identity'nin kendi kuralları + PasswordPolicyValidator) birlikte dener.
/// Veritabanına bağlanmaz; Docker gerekmez.
/// </summary>
public sealed class PasswordPolicyTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly UserManager<ApplicationUser> _users;

    public PasswordPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure("Host=localhost;Database=unused");
        services.AddScoped<ICurrentUser, TestUser>();
        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    [Theory]
    [InlineData("kirmizi-kalem-42")]
    [InlineData("Tezgah2026Usta")]
    [InlineData("çiçekli tamir 7")]
    public async Task Long_password_with_letter_and_digit_is_accepted(string password)
    {
        Assert.Empty(await ErrorsAsync(password));
    }

    [Theory]
    [InlineData("kisa1abc", "PasswordTooShort")]
    [InlineData("sadeceharfler", "PasswordRequiresDigit")]
    [InlineData("1234567890123", PasswordPolicyValidator.RequiresLetterCode)]
    [InlineData("aaaaaaaaaa1", "PasswordRequiresUniqueChars")]
    [InlineData("1q2w3e4r5t", PasswordPolicyValidator.TooCommonCode)]
    [InlineData("Password123", PasswordPolicyValidator.TooCommonCode)]
    public async Task Weak_password_is_rejected_with_its_reason(string password, string expectedCode)
    {
        Assert.Contains(expectedCode, await ErrorsAsync(password));
    }

    private async Task<List<string>> ErrorsAsync(string password)
    {
        var user = new ApplicationUser { UserName = "usta@example.com", Email = "usta@example.com" };
        var codes = new List<string>();
        foreach (var validator in _users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(_users, user, password);
            codes.AddRange(result.Errors.Select(e => e.Code));
        }
        return codes;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
    }
}
