using System.Collections.Concurrent;
using Shelter.BuildingBlocks.Communications;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>Collects sent emails instead of sending them; the default sender of <see cref="ShelterApiFactory"/>.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Emails sent so far, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    /// <summary>Emails sent to <paramref name="to"/>, oldest first.</summary>
    public IReadOnlyList<EmailMessage> SentTo(string to) =>
        [.. _sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase))];

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
