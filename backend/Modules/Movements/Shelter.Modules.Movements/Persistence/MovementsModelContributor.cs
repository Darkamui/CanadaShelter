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
        builder.HasIndex(s => new { s.TenantId, s.Code }).IsUnique();
    }
}
