namespace Shelter.BuildingBlocks.Auditing;

/// <summary>Who caused an audited change.</summary>
public enum AuditActorType
{
    /// <summary>The platform itself (jobs, migrations, integrations without a user).</summary>
    System,

    /// <summary>An unauthenticated caller. Every request until staff authentication exists (M2).</summary>
    Anonymous,

    /// <summary>An authenticated staff user.</summary>
    User,
}

/// <summary>
/// Actor, source and correlation of the current unit of work, stamped on every audit event (architecture §17.1).
/// Scoped. Set once by the request pipeline or the job runner; defaults to the system actor.
/// </summary>
public sealed class AuditContext
{
    internal const int MaxSourceLength = 100;
    internal const int MaxCorrelationIdLength = 64;

    /// <summary>Kind of actor.</summary>
    public AuditActorType ActorType { get; private set; } = AuditActorType.System;

    /// <summary>The actor's ID, when the actor is a user.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Where the change came from: <c>api</c>, <c>job:&lt;name&gt;</c>, <c>system</c>, ….</summary>
    public string Source { get; private set; } = AuditSources.System;

    /// <summary>Correlation ID of the request or job; never personal data.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>Sets the actor, source and correlation of this scope.</summary>
    public void Set(AuditActorType actorType, Guid? actorId, string source, string? correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (source.Length > MaxSourceLength)
        {
            throw new ArgumentException($"Source must be at most {MaxSourceLength} characters.", nameof(source));
        }

        if (correlationId is { Length: > MaxCorrelationIdLength })
        {
            throw new ArgumentException($"Correlation ID must be at most {MaxCorrelationIdLength} characters.", nameof(correlationId));
        }

        if (actorType == AuditActorType.User && actorId is null)
        {
            throw new ArgumentException("A user actor needs an ID.", nameof(actorId));
        }

        ActorType = actorType;
        ActorId = actorId;
        Source = source;
        CorrelationId = correlationId;
    }
}
