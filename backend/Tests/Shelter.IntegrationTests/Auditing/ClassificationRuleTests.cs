using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;
using Shelter.Testing;
using Shelter.Testing.Auditing;

namespace Shelter.IntegrationTests.Auditing;

/// <summary>
/// M1-6 (ADR 0011): every entity linked to a person classifies all of its fields and names its audit subject. Runs
/// over the model the Host composes from every module, so a new module entity breaking the rule fails here.
/// </summary>
public sealed class ClassificationRuleTests
{
    [Fact]
    public async Task Composed_model_classifies_every_field_of_person_linked_entities()
    {
        await using var factory = new ShelterApiFactory(ShelterApiFactory.UnreachableDatabase);
        await using var scope = factory.Services.CreateAsyncScope();

        var model = scope.ServiceProvider.GetRequiredService<ShelterDbContext>().Model;

        Assert.Empty(AuditClassificationRules.FindViolations(model));
    }

    [Fact]
    public async Task Unclassified_field_on_a_person_is_a_violation()
    {
        await using var db = TestDbContexts.Create(ShelterApiFactory.UnreachableDatabase, tenantId: null, new SamplePersonModelContributor());

        var violation = Assert.Single(AuditClassificationRules.FindViolations(db.Model));
        Assert.Contains("SamplePerson.Nickname is not classified", violation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Person_linked_entity_without_subject_or_classification_is_a_violation()
    {
        await using var db = TestDbContexts.Create(ShelterApiFactory.UnreachableDatabase, tenantId: null, new CareNoteContributor());

        var violations = AuditClassificationRules.FindViolations(db.Model);

        Assert.Contains(violations, v => v.Contains("CareNote.OwnerPersonId is not classified", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("CareNote has personal fields but no audit subject", StringComparison.Ordinal));
    }

    /// <summary>A record about a person that forgot its subject and one classification.</summary>
    private sealed class CareNote : ITenantOwned
    {
        public Guid Id { get; private set; }

        public Guid TenantId { get; private set; }

        public Guid OwnerPersonId { get; private set; }

        public string Note { get; private set; } = string.Empty;
    }

    private sealed class CareNoteContributor : IModelContributor
    {
        public void ConfigureModel(ModelBuilder modelBuilder)
        {
            var builder = modelBuilder.Entity<CareNote>();
            builder.ToTable("care_note", "testing");
            builder.Property(n => n.Note).IsPersonalData();
        }
    }
}
