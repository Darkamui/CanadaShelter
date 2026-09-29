using Shelter.Modules.People.Domain;
using Shelter.Modules.People.Features.People;
using Shelter.Testing;

namespace Shelter.Modules.People.Tests;

/// <summary>M3-2: people are tenant-isolated, and the directory contract sees only the caller's organization.</summary>
public sealed class PeopleIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public Task Person_is_isolated() =>
        fixture.Tenants.AssertIsolatedAsync(() => NewPerson("Isolation"), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Directory_resolves_only_the_callers_people()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = TenantHarness.NewTenantId();
        var tenantB = TenantHarness.NewTenantId();
        var mine = await fixture.Tenants.SeedAsync(tenantA, NewPerson("Mine"), ct);
        var archived = NewPerson("Archived");
        archived.Archive(DateTimeOffset.UtcNow);
        archived = await fixture.Tenants.SeedAsync(tenantA, archived, ct);
        var theirs = await fixture.Tenants.SeedAsync(tenantB, NewPerson("Theirs"), ct);

        var (summaries, mineActive, archivedActive, theirsActive) = await fixture.InTenantAsync(tenantA, async db =>
        {
            var directory = new PersonDirectory(db);
            return (
                await directory.GetSummariesAsync([mine.Id, archived.Id, theirs.Id, mine.Id], ct),
                await directory.ExistsActiveAsync(mine.Id, ct),
                await directory.ExistsActiveAsync(archived.Id, ct),
                await directory.ExistsActiveAsync(theirs.Id, ct));
        });

        Assert.Equal(new HashSet<Guid> { archived.Id, mine.Id }, summaries.Keys.ToHashSet());
        Assert.Equal("Mine", summaries[mine.Id].DisplayName);
        Assert.True(summaries[archived.Id].IsArchived);
        Assert.True(mineActive);
        Assert.False(archivedActive);
        Assert.False(theirsActive);
    }

    private static Person NewPerson(string name) =>
        new(new PersonDetails(name, null, null, null, null, null, null, null, null, null, "fr", [], null), DateTimeOffset.UtcNow);
}
