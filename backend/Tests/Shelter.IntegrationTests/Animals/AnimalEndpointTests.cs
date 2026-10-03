using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.IntegrationTests.Infrastructure;
using Shelter.Modules.Animals.Contracts;

namespace Shelter.IntegrationTests.Animals;

/// <summary>
/// M3-4: staff register and edit animals. Numbers are sequential without gaps even under concurrency, an active
/// microchip belongs to one animal of the organization, a hidden species is refused for new choices but kept on
/// existing animals, edits are optimistic, and the timeline is append-only and resolves names when read.
/// </summary>
public sealed class AnimalEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string AnimalsPath = "/api/animals";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organization = Guid.CreateVersion7();

    [Fact]
    public async Task Create_registers_a_not_in_care_animal_with_its_microchip_and_a_timeline_event()
    {
        using var client = await StaffClientAsync();

        var animal = await CreateAsync(client, new
        {
            name = " Éclair ",
            speciesCode = "cat",
            breed = "Européen",
            sex = "female",
            birthDate = "2024-05-01",
            birthDateEstimated = true,
            medicalAlert = "Diabétique",
            microchip = "900 123 456 789 012",
        });

        Assert.Equal(1, animal.Number);
        Assert.Equal("Éclair", animal.Name);
        Assert.Equal("female", animal.Sex);
        Assert.Equal("unknown", animal.ReproductiveStatus);
        Assert.Equal(CustodyStatuses.NotInCare, animal.CustodyStatus);
        Assert.True(animal.BirthDateEstimated);
        var chip = Assert.Single(animal.Identifiers);
        Assert.Equal("microchip", chip.Type);
        Assert.Equal("900 123 456 789 012", chip.Value);

        var timeline = await TimelineAsync(client, animal.Id);
        var registered = Assert.Single(timeline.Items);
        Assert.Equal("animal_registered", registered.Type);
        Assert.Equal("animals", registered.SourceModule);
        Assert.Equal(animal.Id, registered.SourceRecordId);
    }

    [Fact]
    public async Task Invalid_fields_are_reported_by_field()
    {
        using var client = await StaffClientAsync();

        var problem = await PostInvalidAsync(client, AnimalsPath, new { speciesCode = "unicorn", sex = "other", birthDate = "2999-01-01", microchip = "--" });

        Assert.Equal(
            new HashSet<string> { "speciesCode", "sex", "birthDate", "microchip" },
            problem.Errors.Keys.ToHashSet());
    }

    [Fact]
    public async Task Concurrent_creations_get_sequential_numbers_without_gaps()
    {
        using var client = await StaffClientAsync();

        var created = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => CreateAsync(client, new { name = $"Chaton {i}", speciesCode = "cat" })));

        Assert.Equal(Enumerable.Range(1, 10), created.Select(a => a.Number).Order());
    }

    [Fact]
    public async Task An_active_microchip_belongs_to_one_animal_until_it_is_deactivated()
    {
        using var client = await StaffClientAsync();
        var first = await CreateAsync(client, new { name = "Un", speciesCode = "dog", microchip = "900123456789012" });

        var duplicateOnCreate = await PostInvalidAsync(client, AnimalsPath, new { name = "Deux", speciesCode = "dog", microchip = "900 123-456 789 012" });
        Assert.Contains("microchip", duplicateOnCreate.Errors.Keys);

        var second = await CreateAsync(client, new { name = "Deux", speciesCode = "dog" });
        var duplicateOnAdd = await PostInvalidAsync(
            client, $"{AnimalsPath}/{second.Id}/identifiers", new { type = "microchip", value = "900123456789012" });
        Assert.Contains("value", duplicateOnAdd.Errors.Keys);

        // The same number as a licence is not a microchip: allowed.
        using (var licence = await client.PostAsJsonAsync(
            new Uri($"{AnimalsPath}/{second.Id}/identifiers", UriKind.Relative),
            new { type = "licence", value = "900123456789012" },
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, licence.StatusCode);
        }

        var chip = first.Identifiers.Single();
        Assert.Equal(HttpStatusCode.NoContent, await DeactivateAsync(client, first.Id, chip.Id));
        Assert.Equal(HttpStatusCode.NoContent, await DeactivateAsync(client, first.Id, chip.Id));
        Assert.Equal(HttpStatusCode.NotFound, await DeactivateAsync(client, second.Id, chip.Id));

        using (var moved = await client.PostAsJsonAsync(
            new Uri($"{AnimalsPath}/{second.Id}/identifiers", UriKind.Relative),
            new { type = "microchip", value = "900123456789012" },
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Created, moved.StatusCode);
        }

        var reloaded = await GetAsync(client, first.Id);
        Assert.False(reloaded.Identifiers.Single().IsActive);
        Assert.NotNull(reloaded.Identifiers.Single().DeactivatedAt);
    }

    [Fact]
    public async Task Search_matches_name_without_accents_number_and_active_identifier()
    {
        using var client = await StaffClientAsync();
        var eclair = await CreateAsync(client, new { name = "Éclair", speciesCode = "cat", microchip = "ABC-123" });
        var rex = await CreateAsync(client, new { name = "Rex", speciesCode = "dog" });

        Assert.Equal([eclair.Id], (await ListAsync(client, "q=ECLA")).Items.Select(a => a.Id));
        Assert.Equal([rex.Id], (await ListAsync(client, $"q=%23{rex.Number}")).Items.Select(a => a.Id));
        Assert.Equal([eclair.Id], (await ListAsync(client, "q=abc123")).Items.Select(a => a.Id));
        Assert.Equal([rex.Id], (await ListAsync(client, "species=dog")).Items.Select(a => a.Id));
        Assert.Equal(2, (await ListAsync(client, $"status={CustodyStatuses.NotInCare}")).TotalCount);
        Assert.Equal(0, (await ListAsync(client, $"status={CustodyStatuses.InCare}")).TotalCount);
        Assert.Equal([rex.Id, eclair.Id], (await ListAsync(client, string.Empty)).Items.Select(a => a.Id));
        Assert.Equal([eclair.Id, rex.Id], (await ListAsync(client, "sort=name")).Items.Select(a => a.Id));

        var page = await ListAsync(client, "pageSize=1&page=2");
        Assert.Equal([eclair.Id], page.Items.Select(a => a.Id));
        Assert.Equal(2, page.TotalCount);

        using var badStatus = await client.GetAsync(new Uri($"{AnimalsPath}?status=lost", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task Hidden_species_is_refused_for_new_choices_but_kept_on_existing_animals()
    {
        using var client = await StaffClientAsync();
        var rabbit = await CreateAsync(client, new { name = "Panpan", speciesCode = "rabbit" });
        await HideSpeciesAsync("rabbit");

        var refused = await PostInvalidAsync(client, AnimalsPath, new { name = "Autre", speciesCode = "rabbit" });
        Assert.Contains("speciesCode", refused.Errors.Keys);

        using (var kept = await PutAsync(client, rabbit.Id, new { name = "Panpan II", speciesCode = "rabbit", version = rabbit.Version }))
        {
            Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
        }

        var cat = await CreateAsync(client, new { name = "Minou", speciesCode = "cat" });
        using (var changedTo = await PutAsync(client, cat.Id, new { name = "Minou", speciesCode = "rabbit", version = cat.Version }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, changedTo.StatusCode);
        }

        var shown = await GetAsync(client, rabbit.Id);
        Assert.Equal("rabbit", shown.SpeciesCode);
        Assert.Equal("Panpan II", shown.Name);
        Assert.Contains(rabbit.Id, (await ListAsync(client, "species=rabbit")).Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_conflict()
    {
        using var client = await StaffClientAsync();
        var animal = await CreateAsync(client, new { name = "Rex", speciesCode = "dog" });

        using (var first = await PutAsync(client, animal.Id, new { name = "Rex", speciesCode = "dog", colour = "Noir", version = animal.Version }))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var updated = await first.Content.ReadFromJsonAsync<AnimalDto>(TestContext.Current.CancellationToken);
            Assert.Equal("Noir", updated!.Colour);
            Assert.NotEqual(animal.Version, updated.Version);
        }

        using (var stale = await PutAsync(client, animal.Id, new { name = "Max", speciesCode = "dog", version = animal.Version }))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var problem = await stale.Content.ReadFromJsonAsync<ConflictDto>(TestContext.Current.CancellationToken);
            Assert.Equal("animal.versionConflict", problem!.Code);
        }

        Assert.Equal("Rex", (await GetAsync(client, animal.Id)).Name);

        using var missing = await PutAsync(client, Guid.NewGuid(), new { name = "X", speciesCode = "dog", version = 1 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Update_never_changes_the_custody_summary()
    {
        using var client = await StaffClientAsync();
        var animal = await CreateAsync(client, new { name = "Rex", speciesCode = "dog" });

        using var response = await PutAsync(client, animal.Id, new
        {
            name = "Rex",
            speciesCode = "dog",
            custodyStatus = CustodyStatuses.InCare,
            currentLocationId = Guid.NewGuid(),
            version = animal.Version,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await GetAsync(client, animal.Id);
        Assert.Equal(CustodyStatuses.NotInCare, reloaded.CustodyStatus);
        Assert.Null(reloaded.CurrentLocationId);
    }

    [Fact]
    public async Task Timeline_resolves_location_and_person_names_when_read()
    {
        using var client = await StaffClientAsync();
        var animal = await CreateAsync(client, new { name = "Rex", speciesCode = "dog" });
        var location = await PostCreatedAsync<IdDto>(client, "/api/operations/locations", new { kindCode = "kennel", name = "Enclos 4" });
        var person = (await PostCreatedAsync<CreatedPersonDto>(client, "/api/people", new { displayName = "Marie Tremblay" })).Person;

        await InOrganizationAsync(async services =>
        {
            services.GetRequiredService<IAnimalTimeline>().Append(new TimelineEntry(
                animal.Id,
                "intake_recorded",
                DateTimeOffset.UtcNow,
                "movements",
                Guid.CreateVersion7(),
                new Dictionary<string, string>
                {
                    ["reasonCode"] = "stray",
                    ["toLocationId"] = location.Id.ToString(),
                    ["personId"] = person.Id.ToString(),
                }));
            await services.GetRequiredService<ShelterDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var timeline = await TimelineAsync(client, animal.Id);

        Assert.Equal(["intake_recorded", "animal_registered"], timeline.Items.Select(e => e.Type));
        var intake = timeline.Items[0];
        Assert.Equal("stray", intake.Parameters["reasonCode"]);
        Assert.Equal("Enclos 4", intake.Names["toLocationId"]);
        Assert.Equal("Marie Tremblay", intake.Names["personId"]);
        Assert.False(intake.Names.ContainsKey("reasonCode"));
    }

    [Fact]
    public async Task Timeline_events_cannot_be_updated_or_deleted_by_the_runtime_role()
    {
        using var client = await StaffClientAsync();
        var animal = await CreateAsync(client, new { name = "Rex", speciesCode = "dog" });

        foreach (var sql in new[]
        {
            $"UPDATE animals.timeline_event SET type = 'tampered' WHERE animal_id = '{animal.Id}'",
            $"DELETE FROM animals.timeline_event WHERE animal_id = '{animal.Id}'",
        })
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => InOrganizationAsync(services =>
                services.GetRequiredService<ShelterDbContext>().Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken)));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        Assert.Equal("animal_registered", Assert.Single((await TimelineAsync(client, animal.Id)).Items).Type);
    }

    [Fact]
    public async Task Unknown_animal_is_not_found()
    {
        using var client = await StaffClientAsync();
        var id = Guid.NewGuid();

        using var get = await client.GetAsync(new Uri($"{AnimalsPath}/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var timeline = await client.GetAsync(new Uri($"{AnimalsPath}/{id}/timeline", UriKind.Relative), TestContext.Current.CancellationToken);
        using var identifier = await client.PostAsJsonAsync(
            new Uri($"{AnimalsPath}/{id}/identifiers", UriKind.Relative), new { type = "licence", value = "123" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, timeline.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, identifier.StatusCode);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> StaffClientAsync()
    {
        var staff = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, "staff");
        return await _factory.CreateAntiforgeryClientAsync(staff, _organization);
    }

    // Runs work in this organization's unit of work, with the host's scoped services (as an endpoint does).
    private async Task InOrganizationAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_organization);
        await scope.ServiceProvider.GetRequiredService<UnitOfWork>().ExecuteAsync(
            async _ =>
            {
                await work(scope.ServiceProvider);
                return 0;
            },
            static _ => true,
            TestContext.Current.CancellationToken);
    }

    // The override table has no endpoint yet (ADR 0017): hide a system species for this organization directly.
    private Task HideSpeciesAsync(string code)
    {
        var overrideId = Guid.CreateVersion7();
        return InOrganizationAsync(services => services.GetRequiredService<ShelterDbContext>().Database.ExecuteSqlAsync(
            $"""
            INSERT INTO animals.species_override (id, tenant_id, code, label_fr, label_en, sort_order, is_hidden)
            SELECT {overrideId}, {_organization}, code, label_fr, label_en, sort_order, TRUE
            FROM animals.species WHERE code = {code}
            """,
            TestContext.Current.CancellationToken));
    }

    private static Task<AnimalDto> CreateAsync(HttpClient client, object body) => PostCreatedAsync<AnimalDto>(client, AnimalsPath, body);

    private static async Task<T> PostCreatedAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<ValidationProblemDto> PostInvalidAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, object body) =>
        client.PutAsJsonAsync(new Uri($"{AnimalsPath}/{id}", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private static async Task<AnimalDto> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AnimalDto>(new Uri($"{AnimalsPath}/{id}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<PageDto<AnimalListItemDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PageDto<AnimalListItemDto>>(new Uri($"{AnimalsPath}?{query}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<PageDto<TimelineItemDto>> TimelineAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<PageDto<TimelineItemDto>>(new Uri($"{AnimalsPath}/{id}/timeline", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<HttpStatusCode> DeactivateAsync(HttpClient client, Guid animalId, Guid identifierId)
    {
        using var response = await client.PostAsync(
            new Uri($"{AnimalsPath}/{animalId}/identifiers/{identifierId}/deactivate", UriKind.Relative), content: null, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private sealed record IdDto(Guid Id);

    private sealed record CreatedPersonDto(IdDto Person);

    private sealed record IdentifierDto(Guid Id, string Type, string Value, bool IsActive, DateTimeOffset? DeactivatedAt);

    private sealed record AnimalDto(
        Guid Id,
        int Number,
        string? Name,
        string SpeciesCode,
        string? Colour,
        string Sex,
        string ReproductiveStatus,
        bool BirthDateEstimated,
        string CustodyStatus,
        Guid? CurrentLocationId,
        List<IdentifierDto> Identifiers,
        uint Version);

    private sealed record AnimalListItemDto(Guid Id, int Number, string? Name);

    private sealed record TimelineItemDto(
        string Type, string SourceModule, Guid? SourceRecordId, Dictionary<string, string> Parameters, Dictionary<string, string> Names);

    private sealed record PageDto<T>(List<T> Items, int Page, int PageSize, int TotalCount);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);

    private sealed record ConflictDto(string Code);
}
