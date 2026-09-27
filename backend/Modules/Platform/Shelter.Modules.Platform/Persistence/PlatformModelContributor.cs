using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.Platform.Domain;

namespace Shelter.Modules.Platform.Persistence;

/// <summary>The Platform module's share of the composed model, in schema <c>platform</c>.</summary>
internal sealed class PlatformModelContributor : IModelContributor
{
    public const string Schema = "platform";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSettingConfiguration());
    }
}

/// <summary>Global table: the tenant registry itself.</summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organization", PlatformModelContributor.Schema);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(63).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();
    }
}

/// <summary>
/// Tenant configuration values: codes and switches, never personal data. Key and Value are audited in plain form,
/// so a setting must never hold free text about a person (a contact email belongs on its own classified entity).
/// </summary>
internal sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_setting", PlatformModelContributor.Schema);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Key).HasMaxLength(100).IsRequired().IsNonPersonalData();
        builder.Property(s => s.Value).HasMaxLength(4000).IsRequired().IsNonPersonalData();
        builder.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
    }
}
