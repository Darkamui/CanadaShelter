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
