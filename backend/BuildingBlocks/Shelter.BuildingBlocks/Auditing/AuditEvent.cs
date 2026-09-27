using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// One row of the audit history (architecture §7.1, §17.1): who did what to which record, when, and the
/// before/after values of the fields involved. Append-only: the runtime role may only insert and select.
/// Values of personal fields are stored only encrypted with the subject's data key; values of unclassified fields
/// are never stored.
/// </summary>
internal sealed class AuditEvent : ITenantOwned
{
    public const string Schema = "audit";
    public const string Table = "audit_event";

    public AuditEvent(
        Guid tenantId,
        AuditContext context,
        string entityType,
        string entityId,
        Guid? subjectId,
        string action,
        string? beforeJson,
        string? afterJson,
        string? metadata,
        DateTimeOffset timestampUtc)
    {
        Id = Guid.CreateVersion7(timestampUtc);
        TenantId = tenantId;
        ActorId = context.ActorId;
        ActorType = context.ActorType;
        Source = context.Source;
        CorrelationId = context.CorrelationId;
        EntityType = entityType;
        EntityId = entityId;
        SubjectId = subjectId;
        Action = action;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        Metadata = metadata;
        TimestampUtc = timestampUtc;
    }

    // EF materialization.
    private AuditEvent()
    {
        EntityType = null!;
        EntityId = null!;
        Action = null!;
        Source = null!;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? ActorId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public string EntityType { get; private set; }

    public string EntityId { get; private set; }

    /// <summary>The person whose data key encrypts this event's personal values (ADR 0011); null if none.</summary>
    public Guid? SubjectId { get; private set; }

    public string Action { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public DateTimeOffset TimestampUtc { get; private set; }

    public string? CorrelationId { get; private set; }

    public string Source { get; private set; }

    public string? Metadata { get; private set; }

    /// <summary>
    /// Maps <c>audit.audit_event</c>. Applied by <see cref="Persistence.ShelterDbContext"/> itself, so every composed
    /// model has it.
    /// </summary>
    public static void Configure(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AuditEvent>();
        builder.ToTable(Table, Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.Property(e => e.BeforeJson).HasColumnType("jsonb");
        builder.Property(e => e.AfterJson).HasColumnType("jsonb");
        builder.Property(e => e.Metadata).HasColumnType("jsonb");
        builder.Property(e => e.TimestampUtc).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(AuditContext.MaxCorrelationIdLength);
        builder.Property(e => e.Source).HasMaxLength(AuditContext.MaxSourceLength).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId, e.TimestampUtc });
        builder.HasIndex(e => new { e.TenantId, e.SubjectId });
    }
}
