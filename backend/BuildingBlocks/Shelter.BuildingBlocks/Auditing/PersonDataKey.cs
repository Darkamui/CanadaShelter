using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Auditing;

/// <summary>
/// A person's data key, wrapped by the master key (ADR 0011, 0016). One per tenant and subject. Shredding sets
/// <see cref="WrappedKey"/> to null and keeps the row as a tombstone, so a shredded subject never gets a new key.
/// Mapped for migrations only: read and written with SQL by <see cref="PersonDataKeyStore"/>, never tracked (so
/// never audited in turn).
/// </summary>
internal sealed class PersonDataKey : ITenantOwned
{
    public const string Table = "person_data_key";

    private PersonDataKey()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SubjectId { get; private set; }

    public byte[]? WrappedKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ShreddedAt { get; private set; }

    /// <summary>Maps <c>audit.person_data_key</c>; applied by <see cref="ShelterDbContext"/> itself.</summary>
    public static void Configure(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<PersonDataKey>();
        builder.ToTable(Table, AuditEvent.Schema);
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.HasIndex(k => new { k.TenantId, k.SubjectId }).IsUnique();
    }
}

/// <summary>Gets, creates and shreds data keys, in the caller's transaction (so under its tenant's RLS).</summary>
internal static class PersonDataKeyStore
{
    /// <summary>The subject's data key, created on first use; null once shredded.</summary>
    public static async Task<byte[]?> GetOrCreateAsync(
        ShelterDbContext db, IKeyProvider keys, DataKeyContext subject, CancellationToken cancellationToken)
    {
        var row = await FindAsync(db, subject, cancellationToken);
        if (row is null)
        {
            var dataKey = RandomNumberGenerator.GetBytes(AesGcmEnvelope.KeySize);
            var wrapped = await keys.WrapKeyAsync(dataKey, subject, cancellationToken);
            var id = Guid.CreateVersion7();
            var now = DateTimeOffset.UtcNow;

            // A concurrent first write for the same subject may win; either way, read back the key that was kept.
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO audit.person_data_key (id, tenant_id, subject_id, wrapped_key, created_at)
                VALUES ({id}, {subject.TenantId}, {subject.SubjectId}, {wrapped}, {now})
                ON CONFLICT (tenant_id, subject_id) DO NOTHING
                """,
                cancellationToken);

            row = await FindAsync(db, subject, cancellationToken)
                ?? throw new InvalidOperationException("The data key could not be stored.");
        }

        return await UnwrapAsync(keys, row, subject, cancellationToken);
    }

    /// <summary>The subject's data key; null if it never had one or it was shredded.</summary>
    public static async Task<byte[]?> FindKeyAsync(
        ShelterDbContext db, IKeyProvider keys, DataKeyContext subject, CancellationToken cancellationToken) =>
        await FindAsync(db, subject, cancellationToken) is { } row
            ? await UnwrapAsync(keys, row, subject, cancellationToken)
            : null;

    /// <summary>Destroys the subject's key, or records a tombstone if it had none. Idempotent.</summary>
    public static Task ShredAsync(ShelterDbContext db, DataKeyContext subject, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        return db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO audit.person_data_key (id, tenant_id, subject_id, wrapped_key, created_at, shredded_at)
            VALUES ({id}, {subject.TenantId}, {subject.SubjectId}, NULL, {now}, {now})
            ON CONFLICT (tenant_id, subject_id) DO UPDATE
            SET wrapped_key = NULL,
                shredded_at = COALESCE(audit.person_data_key.shredded_at, EXCLUDED.shredded_at)
            """,
            cancellationToken);
    }

    private static async Task<KeyRow?> FindAsync(ShelterDbContext db, DataKeyContext subject, CancellationToken cancellationToken) =>
        (await db.Database
            .SqlQuery<KeyRow>(
                $"""
                SELECT wrapped_key, shredded_at
                FROM audit.person_data_key
                WHERE tenant_id = {subject.TenantId} AND subject_id = {subject.SubjectId}
                """)
            .ToListAsync(cancellationToken))
        .SingleOrDefault();

    private static async Task<byte[]?> UnwrapAsync(IKeyProvider keys, KeyRow row, DataKeyContext subject, CancellationToken cancellationToken) =>
        row.WrappedKey is null ? null : await keys.UnwrapKeyAsync(row.WrappedKey, subject, cancellationToken);

    private sealed record KeyRow(byte[]? WrappedKey, DateTimeOffset? ShreddedAt);
}
