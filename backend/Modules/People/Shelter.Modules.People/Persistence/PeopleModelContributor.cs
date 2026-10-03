using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.Modules.People.Domain;

namespace Shelter.Modules.People.Persistence;

/// <summary>The People module's share of the composed model, in schema <c>people</c>.</summary>
internal sealed class PeopleModelContributor : IModelContributor
{
    public const string Schema = "people";

    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
    }
}

/// <summary>
/// People: tenant-owned, RLS. Every descriptive column is personal, the derived search columns included, with the
/// person as audit subject: shredding the person's key erases their audit history (ADR 0011).
/// </summary>
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("person", PeopleModelContributor.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever().IsNonPersonalData();
        builder.Property(p => p.TenantId).IsNonPersonalData();
        builder.HasAuditSubject(p => p.Id);

        builder.Property(p => p.FirstName).HasMaxLength(100).IsPersonalData();
        builder.Property(p => p.LastName).HasMaxLength(100).IsPersonalData();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired().IsPersonalData();
        builder.Property(p => p.Email).HasMaxLength(256).IsPersonalData();
        builder.Property(p => p.Phone).HasMaxLength(50).IsPersonalData();
        builder.Property(p => p.SecondaryPhone).HasMaxLength(50).IsPersonalData();
        builder.Property(p => p.AddressLine).HasMaxLength(200).IsPersonalData();
        builder.Property(p => p.City).HasMaxLength(100).IsPersonalData();
        builder.Property(p => p.Province).HasMaxLength(2).IsPersonalData();
        builder.Property(p => p.PostalCode).HasMaxLength(10).IsPersonalData();
        builder.Property(p => p.PreferredLanguage).HasMaxLength(2).IsRequired().IsPersonalData();
        builder.Property(p => p.RoleTags).IsRequired().HasDefaultValueSql("'{}'").IsPersonalData();
        builder.Property(p => p.Notes).HasMaxLength(4000).IsPersonalData();

        builder.Property(p => p.SearchText).HasMaxLength(800).IsRequired().IsPersonalData();
        builder.Property(p => p.NormalizedEmail).HasMaxLength(256).IsPersonalData();
        builder.Property(p => p.PhoneDigits).HasMaxLength(50).IsPersonalData();
        builder.Property(p => p.SecondaryPhoneDigits).HasMaxLength(50).IsPersonalData();

        builder.Property(p => p.IsArchived).IsRequired().IsNonPersonalData();
        builder.Property(p => p.CreatedAt).IsRequired().IsNonPersonalData();
        builder.Property(p => p.UpdatedAt).IsRequired().IsNonPersonalData();

        // List order (name, then ID as tie-breaker) and duplicate hints.
        builder.HasIndex(p => new { p.TenantId, p.IsArchived, p.DisplayName, p.Id });
        builder.HasIndex(p => new { p.TenantId, p.NormalizedEmail });
        builder.HasIndex(p => new { p.TenantId, p.PhoneDigits });
        builder.HasIndex(p => new { p.TenantId, p.SecondaryPhoneDigits });

        // Contains-search. A GIN index cannot lead with tenant_id without btree_gin; RLS still filters (migration).
        builder.HasIndex(p => p.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
