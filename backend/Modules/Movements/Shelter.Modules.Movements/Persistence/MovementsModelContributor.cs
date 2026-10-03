using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Persistence;

/// <summary>The Movements module's share of the composed model, in schema <c>movements</c>.</summary>
internal sealed class MovementsModelContributor : IModelContributor
{
    public const string Schema = "movements";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new IntakeReasonConfiguration());
        modelBuilder.ApplyConfiguration(new IntakeReasonOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new OutcomeTypeConfiguration());
        modelBuilder.ApplyConfiguration(new OutcomeTypeOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new MovementConfiguration());
    }
}

/// <summary>Global table: system intake reason, seeded by migrations.</summary>
internal sealed class IntakeReasonConfiguration : IEntityTypeConfiguration<IntakeReason>
{
    public void Configure(EntityTypeBuilder<IntakeReason> builder)
    {
        builder.ToTable("intake_reason", MovementsModelContributor.Schema);
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).HasMaxLength(50).IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(s => s.SacCategory).HasMaxLength(50).IsRequired().IsNonPersonalData();
    }
}

/// <summary>Tenant-owned: one row per changed or added intake reason code.</summary>
internal sealed class IntakeReasonOverrideConfiguration : IEntityTypeConfiguration<IntakeReasonOverride>
{
    public void Configure(EntityTypeBuilder<IntakeReasonOverride> builder)
    {
        builder.ToTable("intake_reason_override", MovementsModelContributor.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(s => s.TenantId).IsNonPersonalData();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(s => s.IsHidden).IsRequired().IsNonPersonalData();
        builder.Property(s => s.SacCategory).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}

/// <summary>Global table: system outcome types, seeded by migrations.</summary>
internal sealed class OutcomeTypeConfiguration : IEntityTypeConfiguration<OutcomeType>
{
    public void Configure(EntityTypeBuilder<OutcomeType> builder)
    {
        builder.ToTable("outcome_type", MovementsModelContributor.Schema);
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).HasMaxLength(50).IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(s => s.SacCategory).HasMaxLength(50).IsRequired().IsNonPersonalData();
    }
}

/// <summary>Tenant-owned: one row per changed or added outcome type code.</summary>
internal sealed class OutcomeTypeOverrideConfiguration : IEntityTypeConfiguration<OutcomeTypeOverride>
{
    public void Configure(EntityTypeBuilder<OutcomeTypeOverride> builder)
    {
        builder.ToTable("outcome_type_override", MovementsModelContributor.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(s => s.TenantId).IsNonPersonalData();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(s => s.IsHidden).IsRequired().IsNonPersonalData();
        builder.Property(s => s.SacCategory).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}

/// <summary>
/// Tenant-owned, append-only movement ledger. Animal, location and person IDs point into other modules, so they have
/// no foreign keys (hard rule 7); only the void's link to the movement it cancels does, and it includes
/// <c>tenant_id</c>. Notes are personal data about the movement's person.
/// </summary>
internal sealed class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.ToTable("movement", MovementsModelContributor.Schema, t =>
        {
            t.HasCheckConstraint("ck_movement_type", "type IN ('intake', 'relocation', 'outcome', 'void')");
            t.HasCheckConstraint(
                "ck_movement_shape",
                """
                (type = 'intake' AND reason_code IS NOT NULL AND to_location_id IS NOT NULL AND from_location_id IS NULL AND voids_movement_id IS NULL)
                OR (type = 'relocation' AND reason_code IS NULL AND from_location_id IS NOT NULL AND to_location_id IS NOT NULL AND from_location_id <> to_location_id AND person_id IS NULL AND voids_movement_id IS NULL)
                OR (type = 'outcome' AND reason_code IS NOT NULL AND from_location_id IS NOT NULL AND to_location_id IS NULL AND voids_movement_id IS NULL)
                OR (type = 'void' AND reason_code IS NULL AND from_location_id IS NULL AND to_location_id IS NULL AND voids_movement_id IS NOT NULL AND notes IS NOT NULL)
                """);
        });
        builder.HasAuditSubject(m => m.PersonId);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(m => m.TenantId).IsNonPersonalData();
        builder.Property(m => m.Type).HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(m => m.AnimalId).IsNonPersonalData();
        builder.Property(m => m.ReasonCode).HasMaxLength(Movement.CodeMaxLength).IsNonPersonalData();
        builder.Property(m => m.FromLocationId).IsNonPersonalData();
        builder.Property(m => m.ToLocationId).IsNonPersonalData();
        builder.Property(m => m.PersonId).IsNonPersonalData();
        builder.Property(m => m.Notes).HasMaxLength(Movement.NotesMaxLength).IsPersonalData();
        builder.Property(m => m.OccurredAt).IsRequired().IsNonPersonalData();
        builder.Property(m => m.RecordedAt).IsRequired().IsNonPersonalData();
        builder.Property(m => m.RecordedByUserId).IsNonPersonalData();
        builder.Property(m => m.VoidsMovementId).IsNonPersonalData();

        // A void cancels a movement of the same organization: the foreign key includes tenant_id, because a plain key
        // check bypasses RLS.
        builder.HasAlternateKey(m => new { m.TenantId, m.Id });
        builder.HasOne<Movement>()
            .WithMany()
            .HasForeignKey(m => new { m.TenantId, m.VoidsMovementId })
            .HasPrincipalKey(m => new { m.TenantId, m.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // An animal's ledger in order (history, latest movement in effect).
        builder.HasIndex(m => new { m.TenantId, m.AnimalId, m.OccurredAt, m.Id });

        // A movement is voided at most once.
        builder.HasIndex(m => new { m.TenantId, m.VoidsMovementId })
            .IsUnique()
            .HasFilter("voids_movement_id IS NOT NULL");
    }
}
