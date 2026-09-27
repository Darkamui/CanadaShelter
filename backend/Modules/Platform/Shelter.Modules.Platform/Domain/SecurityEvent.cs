namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// An account security event (ADR 0018): the audit trail for global accounts, which the tenant audit does not
/// cover. Global and append-only for the runtime role. Holds no email or IP address; the account is named by its
/// ID only, and failed logins for unknown emails have none.
/// </summary>
internal sealed class SecurityEvent
{
    private SecurityEvent()
    {
    }

    public SecurityEvent(Guid? userId, SecurityEventType type, DateTimeOffset occurredAt, string? correlationId)
    {
        Id = Guid.CreateVersion7();
        UserId = userId;
        Type = type;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }

    /// <summary>The account, when one is known.</summary>
    public Guid? UserId { get; private set; }

    public SecurityEventType Type { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Correlation ID of the request; never personal data.</summary>
    public string? CorrelationId { get; private set; }
}

/// <summary>Kinds of <see cref="SecurityEvent"/>. Stored by name.</summary>
internal enum SecurityEventType
{
    LoginSucceeded,
    LoginFailed,
    LockedOut,
    LoggedOut,
    PasswordChanged,
    MfaEnabled,
    MfaDisabled,
    RecoveryCodeUsed,
}
