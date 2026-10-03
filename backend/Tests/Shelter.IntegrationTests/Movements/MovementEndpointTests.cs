using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Shelter.IntegrationTests.Infrastructure;
using Shelter.Modules.Animals.Contracts;

namespace Shelter.IntegrationTests.Movements;

/// <summary>
/// M3-5: the movement ledger drives custody. Each movement, the animal's custody summary and its timeline event commit
/// together or not at all; movements of one animal are serialized; a void restores the summary the ledger projects
/// without the voided movement; and a location with animals in care cannot be archived.
/// </summary>
public sealed class MovementEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string MovementsPath = "/api/movements";

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);
    private readonly Guid _organization = Guid.CreateVersion7();

    [Fact]
    public async Task Intake_relocation_and_outcome_move_the_animal_and_append_to_its_timeline()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        var room = await CreateLocationAsync(client, "Salle 2", kind: "room");
        var adopter = await CreatePersonAsync(client, "Marie Tremblay");

        var intake = await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel, notes = "Trouvé rue Saint-Denis" });
        Assert.Equal("intake", intake.Type);
        Assert.Equal("Enclos 1", intake.ToLocationName);
        Assert.Equal("Trouvé rue Saint-Denis", intake.Notes);
        var inCare = await GetAnimalAsync(client, animal);
        Assert.Equal(CustodyStatuses.InCare, inCare.CustodyStatus);
        Assert.Equal(kennel, inCare.CurrentLocationId);

        var move = await PostCreatedAsync(client, "/relocations", new { animalId = animal, toLocationId = room });
        Assert.Equal(kennel, move.FromLocationId);
        Assert.Equal(room, (await GetAnimalAsync(client, animal)).CurrentLocationId);

        var outcome = await PostCreatedAsync(client, "/outcomes", new { animalId = animal, outcomeCode = "adoption", personId = adopter });
        Assert.Equal(room, outcome.FromLocationId);
        Assert.Equal("Marie Tremblay", outcome.PersonName);
        var gone = await GetAnimalAsync(client, animal);
        Assert.Equal(CustodyStatuses.Outcome, gone.CustodyStatus);
        Assert.Null(gone.CurrentLocationId);

        var history = await ListAsync(client, animal);
        Assert.Equal(["outcome", "relocation", "intake"], history.Select(m => m.Type));
        var timeline = await TimelineAsync(client, animal);
        Assert.Equal(["outcome_recorded", "relocation_recorded", "intake_recorded", "animal_registered"], timeline.Items.Select(e => e.Type));
        Assert.Equal(intake.Id, timeline.Items[2].SourceRecordId);
        Assert.Equal("adoption", timeline.Items[0].Parameters["outcomeCode"]);
    }

    [Fact]
    public async Task Movements_that_custody_does_not_allow_are_conflicts()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");

        await AssertConflictAsync(client, "/relocations", new { animalId = animal, toLocationId = kennel });
        await AssertConflictAsync(client, "/outcomes", new { animalId = animal, outcomeCode = "transfer_out" });

        await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
        await AssertConflictAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });

        // Died: never taken in again.
        await PostCreatedAsync(client, "/outcomes", new { animalId = animal, outcomeCode = "died" });
        await AssertConflictAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
    }

    [Fact]
    public async Task A_live_outcome_allows_a_new_intake()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
        await PostCreatedAsync(client, "/outcomes", new { animalId = animal, outcomeCode = "transfer_out" });

        await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "returned_adoption", toLocationId = kennel });

        Assert.Equal(CustodyStatuses.InCare, (await GetAnimalAsync(client, animal)).CustodyStatus);
    }

    [Fact]
    public async Task Invalid_fields_are_reported_by_field()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        var building = await CreateLocationAsync(client, "Bâtiment A", kind: "building");

        var missing = await PostInvalidAsync(client, "/intakes", new { });
        Assert.Equal(new HashSet<string> { "animalId", "reasonCode", "toLocationId" }, missing.Errors.Keys.ToHashSet());

        var wrong = await PostInvalidAsync(client, "/intakes", new
        {
            animalId = animal,
            reasonCode = "unicorn",
            toLocationId = building,
            personId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow.AddHours(1),
        });
        Assert.Equal(
            new HashSet<string> { "reasonCode", "toLocationId", "personId", "occurredAt" },
            wrong.Errors.Keys.ToHashSet());

        var unknownAnimal = await PostInvalidAsync(client, "/intakes", new { animalId = Guid.NewGuid(), reasonCode = "stray", toLocationId = kennel });
        Assert.Contains("animalId", unknownAnimal.Errors.Keys);

        var intake = await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
        var sameLocation = await PostInvalidAsync(client, "/relocations", new { animalId = animal, toLocationId = kennel });
        Assert.Contains("toLocationId", sameLocation.Errors.Keys);

        var beforeIntake = await PostInvalidAsync(
            client, "/relocations", new { animalId = animal, toLocationId = building, occurredAt = intake.OccurredAt.AddMinutes(-1) });
        Assert.Contains("occurredAt", beforeIntake.Errors.Keys);

        var adoptionWithoutPerson = await PostInvalidAsync(client, "/outcomes", new { animalId = animal, outcomeCode = "adoption" });
        Assert.Contains("personId", adoptionWithoutPerson.Errors.Keys);
    }

    [Fact]
    public async Task Concurrent_intakes_of_one_animal_record_one_and_refuse_the_other()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        var other = await CreateLocationAsync(client, "Enclos 2");

        var responses = await Task.WhenAll(new[] { kennel, other }.Select(location => client.PostAsJsonAsync(
            new Uri($"{MovementsPath}/intakes", UriKind.Relative),
            new { animalId = animal, reasonCode = "stray", toLocationId = location },
            TestContext.Current.CancellationToken)));
        try
        {
            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(r => r.StatusCode).Order());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        Assert.Single(await ListAsync(client, animal));
    }

    [Fact]
    public async Task A_void_restores_the_previous_custody_and_only_the_latest_movement_can_be_voided()
    {
        using var client = await ClientAsync("administrator");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        var room = await CreateLocationAsync(client, "Salle 2", kind: "room");
        var intake = await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
        var move = await PostCreatedAsync(client, "/relocations", new { animalId = animal, toLocationId = room });

        using (var notLatest = await PostAsync(client, $"/{intake.Id}/void", new { reason = "Erreur" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, notLatest.StatusCode);
            Assert.Equal("movement.notLatest", (await notLatest.Content.ReadFromJsonAsync<ConflictDto>(TestContext.Current.CancellationToken))!.Code);
        }

        var noReason = await PostInvalidAsync(client, $"/{move.Id}/void", new { reason = " " });
        Assert.Contains("reason", noReason.Errors.Keys);

        var voidMove = await PostCreatedAsync(client, $"/{move.Id}/void", new { reason = "Mauvais enclos" });
        Assert.Equal("void", voidMove.Type);
        Assert.Equal(move.Id, voidMove.VoidsMovementId);
        Assert.Equal(kennel, (await GetAnimalAsync(client, animal)).CurrentLocationId);

        await PostCreatedAsync(client, $"/{intake.Id}/void", new { reason = "Mauvais animal" });
        var restored = await GetAnimalAsync(client, animal);
        Assert.Equal(CustodyStatuses.NotInCare, restored.CustodyStatus);
        Assert.Null(restored.CurrentLocationId);

        using (var again = await PostAsync(client, $"/{intake.Id}/void", new { reason = "Encore" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        var history = await ListAsync(client, animal);
        Assert.Equal(voidMove.Id, history.Single(m => m.Id == move.Id).VoidedByMovementId);
        Assert.Equal(
            ["movement_voided", "movement_voided", "relocation_recorded", "intake_recorded", "animal_registered"],
            (await TimelineAsync(client, animal)).Items.Select(e => e.Type));

        // The ledger is in effect again from a clean state: a new intake works.
        await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = room });
    }

    [Fact]
    public async Task A_location_with_animals_in_care_cannot_be_archived()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");
        var room = await CreateLocationAsync(client, "Salle 2", kind: "room");
        await PostCreatedAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });

        using (var occupied = await ArchiveAsync(client, kennel))
        {
            Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
            Assert.Equal("location.hasAnimals", (await occupied.Content.ReadFromJsonAsync<ConflictDto>(TestContext.Current.CancellationToken))!.Code);
        }

        await PostCreatedAsync(client, "/relocations", new { animalId = animal, toLocationId = room });
        using (var empty = await ArchiveAsync(client, kennel))
        {
            Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        }

        // Archived: no longer a placement.
        var archivedTarget = await PostInvalidAsync(client, "/relocations", new { animalId = animal, toLocationId = kennel });
        Assert.Contains("toLocationId", archivedTarget.Errors.Keys);
    }

    [Fact]
    public async Task Movement_summary_and_timeline_commit_together_or_not_at_all()
    {
        using var client = await ClientAsync("staff");
        var animal = await CreateAnimalAsync(client);
        var kennel = await CreateLocationAsync(client, "Enclos 1");

        // A deferred constraint trigger fails this animal's movement at commit, after every change was staged and
        // flushed: the request transaction must roll back the summary and the timeline event with it.
        var suffix = animal.ToString("N");
        await MigratorSqlAsync(
            $"""
            CREATE FUNCTION movements.fail_{suffix}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.animal_id = '{animal}' THEN RAISE EXCEPTION 'forced failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE CONSTRAINT TRIGGER fail_{suffix} AFTER INSERT ON movements.movement
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION movements.fail_{suffix}();
            """);
        try
        {
            using var response = await PostAsync(client, "/intakes", new { animalId = animal, reasonCode = "stray", toLocationId = kennel });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            await MigratorSqlAsync(
                $"""
                DROP TRIGGER fail_{suffix} ON movements.movement;
                DROP FUNCTION movements.fail_{suffix}();
                """);
        }

        Assert.Empty(await ListAsync(client, animal));
        var summary = await GetAnimalAsync(client, animal);
        Assert.Equal(CustodyStatuses.NotInCare, summary.CustodyStatus);
        Assert.Null(summary.CurrentLocationId);
        Assert.Equal("animal_registered", Assert.Single((await TimelineAsync(client, animal)).Items).Type);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> ClientAsync(string role)
    {
        var member = await TestMemberships.NewMemberAsync(postgres.AppConnectionString, _organization, role);
        return await _factory.CreateAntiforgeryClientAsync(member, _organization);
    }

    private async Task MigratorSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.Database.MigratorConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> CreateAnimalAsync(HttpClient client) =>
        (await PostJsonAsync<IdDto>(client, "/api/animals", new { name = "Rex", speciesCode = "dog" })).Id;

    private static async Task<Guid> CreateLocationAsync(HttpClient client, string name, string kind = "kennel") =>
        (await PostJsonAsync<IdDto>(client, "/api/operations/locations", new { kindCode = kind, name })).Id;

    private static async Task<Guid> CreatePersonAsync(HttpClient client, string displayName) =>
        (await PostJsonAsync<CreatedPersonDto>(client, "/api/people", new { displayName })).Person.Id;

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync(new Uri($"{MovementsPath}{path}", UriKind.Relative), body, TestContext.Current.CancellationToken);

    private static Task<MovementDto> PostCreatedAsync(HttpClient client, string path, object body) =>
        PostJsonAsync<MovementDto>(client, $"{MovementsPath}{path}", body);

    private static async Task<ValidationProblemDto> PostInvalidAsync(HttpClient client, string path, object body)
    {
        using var response = await PostAsync(client, path, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDto>(TestContext.Current.CancellationToken))!;
    }

    private static async Task AssertConflictAsync(HttpClient client, string path, object body)
    {
        using var response = await PostAsync(client, path, body);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("movement.custodyConflict", (await response.Content.ReadFromJsonAsync<ConflictDto>(TestContext.Current.CancellationToken))!.Code);
    }

    private static Task<HttpResponseMessage> ArchiveAsync(HttpClient client, Guid locationId) =>
        client.PostAsync(new Uri($"/api/operations/locations/{locationId}/archive", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

    private static async Task<List<MovementDto>> ListAsync(HttpClient client, Guid animalId) =>
        (await client.GetFromJsonAsync<List<MovementDto>>(new Uri($"{MovementsPath}?animalId={animalId}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<AnimalDto> GetAnimalAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AnimalDto>(new Uri($"/api/animals/{id}", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private static async Task<PageDto<TimelineItemDto>> TimelineAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<PageDto<TimelineItemDto>>(new Uri($"/api/animals/{id}/timeline", UriKind.Relative), TestContext.Current.CancellationToken))!;

    private sealed record IdDto(Guid Id);

    private sealed record CreatedPersonDto(IdDto Person);

    private sealed record MovementDto(
        Guid Id,
        string Type,
        Guid? FromLocationId,
        Guid? ToLocationId,
        string? ToLocationName,
        Guid? PersonId,
        string? PersonName,
        string? Notes,
        DateTimeOffset OccurredAt,
        Guid? VoidsMovementId,
        Guid? VoidedByMovementId);

    private sealed record AnimalDto(Guid Id, string CustodyStatus, Guid? CurrentLocationId);

    private sealed record TimelineItemDto(string Type, Guid? SourceRecordId, Dictionary<string, string> Parameters);

    private sealed record PageDto<T>(List<T> Items, int Page, int PageSize, int TotalCount);

    private sealed record ValidationProblemDto(Dictionary<string, string[]> Errors);

    private sealed record ConflictDto(string Code);
}
