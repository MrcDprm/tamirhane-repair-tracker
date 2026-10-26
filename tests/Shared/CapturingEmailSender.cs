using Tamirhane.Infrastructure.Email;

namespace Tamirhane.Tests;

/// <summary>
/// E-posta göndermez, saklar; testler gelen kutusuna bakar gibi okur. İki entegrasyon test projesi de kullanır.
/// E-postalar arka plan kuyruğundan gelir: okumadan önce kuyruğun boşalması beklenir.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    /// <summary>Okumadan önce beklenecek iş (fixture kuyruğun boşalmasını bağlar).</summary>
    public Func<Task>? BeforeRead { get; set; }

    public IReadOnlyList<EmailMessage> SentTo(string address)
    {
        BeforeRead?.Invoke().GetAwaiter().GetResult();
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
