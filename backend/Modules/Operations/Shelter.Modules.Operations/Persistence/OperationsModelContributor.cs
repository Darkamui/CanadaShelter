using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Persistence;

/// <summary>The Operations module's share of the composed model, in schema <c>operations</c>.</summary>
internal sealed class OperationsModelContributor : IModelContributor
{
    public const string Schema = "operations";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LocationKindConfiguration());
        modelBuilder.ApplyConfiguration(new LocationKindOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
    }
}

/// <summary>Global table: system location kinds, seeded by migrations.</summary>
internal sealed class LocationKindConfiguration : IEntityTypeConfiguration<LocationKind>
{
    public void Configure(EntityTypeBuilder<LocationKind> builder)
    {
        builder.ToTable("location_kind", OperationsModelContributor.Schema);
        builder.HasKey(k => k.Code);
        builder.Property(k => k.Code).HasMaxLength(50).IsNonPersonalData();
        builder.MapLocalizedText(k => k.Label, "label", 100);
        builder.Property(k => k.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(k => k.HoldsAnimals).IsRequired().IsNonPersonalData();
    }
}

/// <summary>Tenant-owned: one row per changed or added location kind code.</summary>
internal sealed class LocationKindOverrideConfiguration : IEntityTypeConfiguration<LocationKindOverride>
{
    public void Configure(EntityTypeBuilder<LocationKindOverride> builder)
    {
        builder.ToTable("location_kind_override", OperationsModelContributor.Schema);
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(k => k.TenantId).IsNonPersonalData();
        builder.Property(k => k.Code).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.MapLocalizedText(k => k.Label, "label", 100);
        builder.Property(k => k.SortOrder).IsRequired().IsNonPersonalData();
        builder.Property(k => k.IsHidden).IsRequired().IsNonPersonalData();
        builder.Property(k => k.HoldsAnimals).IsRequired().IsNonPersonalData();
        builder.HasIndex(k => new { k.TenantId, k.Code }).IsUnique();
    }
}

/// <summary>Tenant-owned location tree. Names are plain text, not personal data.</summary>
internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("location", OperationsModelContributor.Schema, t =>
            t.HasCheckConstraint("ck_location_capacity", $"capacity IS NULL OR capacity BETWEEN 0 AND {Location.MaxCapacity}"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(l => l.TenantId).IsNonPersonalData();
        builder.Property(l => l.ParentId).IsNonPersonalData();
        builder.Property(l => l.KindCode).HasMaxLength(50).IsRequired().IsNonPersonalData();
        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).IsRequired().IsNonPersonalData();
        builder.Property(l => l.NormalizedName).HasMaxLength(Location.NameMaxLength * 2).IsRequired().IsNonPersonalData();
        builder.Property(l => l.Capacity).IsNonPersonalData();
        builder.Property(l => l.IsArchived).IsRequired().IsNonPersonalData();
        builder.Property(l => l.CreatedAt).IsRequired().IsNonPersonalData();
        builder.Property(l => l.UpdatedAt).IsRequired().IsNonPersonalData();

        // The parent is in the same organization: the foreign key includes tenant_id, because a plain key check
        // bypasses RLS. A NULL parent_id (top level) skips the check (MATCH SIMPLE).
        builder.HasAlternateKey(l => new { l.TenantId, l.Id });
        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(l => new { l.TenantId, l.ParentId })
            .HasPrincipalKey(l => new { l.TenantId, l.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Child lookups (tree walks, foreign key checks), archived rows included.
        builder.HasIndex(l => new { l.TenantId, l.ParentId });

        // Names are unique among active siblings; top-level locations are siblings of each other (NULLS NOT DISTINCT).
        builder.HasIndex(l => new { l.TenantId, l.ParentId, l.NormalizedName })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("NOT is_archived");
    }
}
