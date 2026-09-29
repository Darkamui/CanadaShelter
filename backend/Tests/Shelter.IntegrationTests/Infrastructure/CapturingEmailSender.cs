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

    /// <summary>
    /// Waits until an email to <paramref name="to"/> is sent (from a background job, say), then returns those emails.
    /// Fails after <paramref name="timeout"/>, 15 seconds by default.
    /// </summary>
    public async Task<IReadOnlyList<EmailMessage>> WaitForAsync(string to, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (SentTo(to) is { Count: 0 })
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("No email was sent in time.");
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return SentTo(to);
    }

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
