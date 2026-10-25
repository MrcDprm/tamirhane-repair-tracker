using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Tests;

/// <summary>E-posta göndermez, saklar; testler gelen kutusuna bakar gibi okur. İki entegrasyon test projesi de kullanır.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> SentTo(string address)
    {
        lock (_sent)
            return _sent.Where(m => m.To.Equals(address, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        lock (_sent)
            _sent.Add(message);
        return Task.CompletedTask;
    }
}
