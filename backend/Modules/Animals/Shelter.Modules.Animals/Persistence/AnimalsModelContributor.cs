using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Persistence;

/// <summary>The Animals module's share of the composed model, in schema <c>animals</c>.</summary>
internal sealed class AnimalsModelContributor : IModelContributor
{
    public const string Schema = "animals";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SpeciesConfiguration());
        modelBuilder.ApplyConfiguration(new SpeciesOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new AnimalConfiguration());
        modelBuilder.ApplyConfiguration(new AnimalIdentifierConfiguration());
        modelBuilder.ApplyConfiguration(new AnimalNumberCounterConfiguration());
        modelBuilder.ApplyConfiguration(new TimelineEventConfiguration());
    }
}

/// <summary>Global table: system species, seeded by migrations.</summary>
internal sealed class SpeciesConfiguration : IEntityTypeConfiguration<Species>
{
    public void Configure(EntityTypeBuilder<Species> builder)
    {
        builder.ToTable("species", AnimalsModelContributor.Schema);
        builder.HasKey(s => s.Code);
        builder.Property(s => s.Code).HasMaxLength(50).IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
    }
}

/// <summary>Tenant-owned: one row per changed or added species code.</summary>
internal sealed class SpeciesOverrideConfiguration : IEntityTypeConfiguration<SpeciesOverride>
{
    public void Configure(EntityTypeBuilder<SpeciesOverride> builder)
    {
        builder.ToTable("species_override", AnimalsModelContributor.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(s => s.TenantId).IsNonPersonalData();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.MapLocalizedText(s => s.Label, "label", 100);
        builder.Property(s => s.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(s => s.IsHidden).IsRequired().IsNonPersonalData();
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}

/// <summary>Tenant-owned animal records. Nothing here is personal data (see <see cref="Animal"/>).</summary>
internal sealed class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("animal", AnimalsModelContributor.Schema, t =>
        {
            t.HasCheckConstraint("ck_animal_number", "number > 0");
            t.HasCheckConstraint("ck_animal_custody", "(custody_status = 'in_care') = (current_location_id IS NOT NULL)");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(a => a.TenantId).IsNonPersonalData();
        builder.Property(a => a.Number).IsRequired().IsNonPersonalData();
        builder.Property(a => a.Name).HasMaxLength(Animal.NameMaxLength).IsNonPersonalData();
        builder.Property(a => a.SpeciesCode).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.Property(a => a.Breed).HasMaxLength(Animal.TextMaxLength).IsNonPersonalData();
        builder.Property(a => a.SecondaryBreed).HasMaxLength(Animal.TextMaxLength).IsNonPersonalData();
        builder.Property(a => a.Colour).HasMaxLength(Animal.TextMaxLength).IsNonPersonalData();
        builder.Property(a => a.Sex).HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(a => a.ReproductiveStatus).HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(a => a.BirthDate).IsNonPersonalData();
        builder.Property(a => a.BirthDateEstimated).IsRequired().IsNonPersonalData();
        // Free text with no audit subject; staff are told to keep personal information out. Audit must not copy these
        // values (docs/modules/animals.md, privacy notes).
        builder.Property(a => a.Marks).HasMaxLength(Animal.NoteMaxLength).IsNonPersonalData();
        builder.Property(a => a.BehaviourAlert).HasMaxLength(Animal.NoteMaxLength).IsNonPersonalData();
        builder.Property(a => a.MedicalAlert).HasMaxLength(Animal.NoteMaxLength).IsNonPersonalData();
        builder.Property(a => a.LegalAlert).HasMaxLength(Animal.NoteMaxLength).IsNonPersonalData();
        builder.Property(a => a.CustodyStatus).HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(a => a.CurrentLocationId).IsNonPersonalData();
        builder.Property(a => a.InCareSince).IsNonPersonalData();
        builder.Property(a => a.CurrentIntakeId).IsNonPersonalData();
        builder.Property(a => a.LastOutcomeCode).HasMaxLength(50).IsNonPersonalData();
        builder.Property(a => a.SearchText).HasMaxLength(Animal.NameMaxLength * 2).IsRequired().IsNonPersonalData();
        builder.Property(a => a.CreatedAt).IsRequired().IsNonPersonalData();
        builder.Property(a => a.UpdatedAt).IsRequired().IsNonPersonalData();
        builder.Property(a => a.Version).IsRowVersion().IsNonPersonalData();
        builder.Ignore(a => a.Custody);

        // Identifiers and timeline events point at (tenant_id, id): a plain key check would bypass RLS.
        builder.HasAlternateKey(a => new { a.TenantId, a.Id });
        builder.HasIndex(a => new { a.TenantId, a.Number }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.CustodyStatus, a.CurrentLocationId });
        builder.HasIndex(a => new { a.TenantId, a.SpeciesCode });

        // Name search ("contains"). A GIN index cannot start with tenant_id without btree_gin; RLS still filters.
        builder.HasIndex(a => a.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

/// <summary>Tenant-owned microchips, licences and external numbers. Not personal data.</summary>
internal sealed class AnimalIdentifierConfiguration : IEntityTypeConfiguration<AnimalIdentifier>
{
    public void Configure(EntityTypeBuilder<AnimalIdentifier> builder)
    {
        builder.ToTable("animal_identifier", AnimalsModelContributor.Schema);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(i => i.TenantId).IsNonPersonalData();
        builder.Property(i => i.AnimalId).IsNonPersonalData();
        builder.Property(i => i.Type).HasMaxLength(20).IsRequired().IsNonPersonalData();
        builder.Property(i => i.Value).HasMaxLength(AnimalIdentifier.ValueMaxLength).IsRequired().IsNonPersonalData();
        builder.Property(i => i.NormalizedValue).HasMaxLength(AnimalIdentifier.ValueMaxLength).IsRequired().IsNonPersonalData();
        builder.Property(i => i.IsActive).IsRequired().IsNonPersonalData();
        builder.Property(i => i.CreatedAt).IsRequired().IsNonPersonalData();
        builder.Property(i => i.DeactivatedAt).IsNonPersonalData();

        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(i => new { i.TenantId, i.AnimalId })
            .HasPrincipalKey(a => new { a.TenantId, a.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.TenantId, i.AnimalId });

        // Search by identifier (exact normalized value).
        builder.HasIndex(i => new { i.TenantId, i.NormalizedValue }, "ix_animal_identifier_tenant_id_normalized_value")
            .HasDatabaseName("ix_animal_identifier_tenant_id_normalized_value");

        // An active microchip belongs to one animal of the organization.
        builder.HasIndex(i => new { i.TenantId, i.NormalizedValue }, "ux_animal_identifier_active_microchip")
            .HasDatabaseName("ux_animal_identifier_active_microchip")
            .IsUnique()
            .HasFilter("is_active AND type = 'microchip'");
    }
}

/// <summary>Tenant-owned: one row per organization, keyed by tenant_id. Written with raw SQL only.</summary>
internal sealed class AnimalNumberCounterConfiguration : IEntityTypeConfiguration<AnimalNumberCounter>
{
    public void Configure(EntityTypeBuilder<AnimalNumberCounter> builder)
    {
        builder.ToTable("animal_number_counter", AnimalsModelContributor.Schema);
        builder.HasKey(c => c.TenantId);
        builder.Property(c => c.TenantId).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(c => c.LastNumber).IsRequired().IsNonPersonalData();
    }
}

/// <summary>Tenant-owned, append-only animal timeline. Parameters are codes and IDs only.</summary>
internal sealed class TimelineEventConfiguration : IEntityTypeConfiguration<TimelineEvent>
{
    public void Configure(EntityTypeBuilder<TimelineEvent> builder)
    {
        builder.ToTable("timeline_event", AnimalsModelContributor.Schema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(e => e.TenantId).IsNonPersonalData();
        builder.Property(e => e.AnimalId).IsNonPersonalData();
        builder.Property(e => e.Type).HasMaxLength(TimelineEvent.TypeMaxLength).IsRequired().IsNonPersonalData();
        builder.Property(e => e.OccurredAt).IsRequired().IsNonPersonalData();
        builder.Property(e => e.RecordedAt).IsRequired().IsNonPersonalData();
        builder.Property(e => e.SourceModule).HasMaxLength(TimelineEvent.SourceModuleMaxLength).IsRequired().IsNonPersonalData();
        builder.Property(e => e.SourceRecordId).IsNonPersonalData();
        builder.Property(e => e.Parameters).HasColumnType("jsonb").IsRequired().IsNonPersonalData();

        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.AnimalId })
            .HasPrincipalKey(a => new { a.TenantId, a.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // One animal's timeline, newest first.
        builder.HasIndex(e => new { e.TenantId, e.AnimalId, e.OccurredAt, e.Id });
    }
}
