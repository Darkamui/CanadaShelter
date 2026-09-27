using System.Net.Http.Json;
using Npgsql;
using Shelter.IntegrationTests.Infrastructure;

namespace Shelter.IntegrationTests.ReferenceData;

/// <summary>
/// M1-7: reference lists return both labels; a tenant's overrides relabel, hide or add values for that tenant only;
/// without a tenant, the system list. Overrides are written with raw SQL as the runtime role, as RLS sees them.
/// </summary>
public sealed class ReferenceDataEndpointTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string InsufficientPrivilege = "42501";

    private static readonly string[] SystemSpecies = ["dog", "cat", "rabbit", "ferret", "small_mammal", "bird", "reptile", "other"];

    private readonly ShelterApiFactory _factory = new(postgres.AppConnectionString);

    [Fact]
    public async Task Species_without_a_tenant_are_the_seeded_system_list_in_both_languages()
    {
        var species = await GetAsync("/api/animals/species", tenantId: null);

        Assert.Equal(SystemSpecies, species.Select(s => s.Code));
        var dog = species[0];
        Assert.Equal(new LabelDto("Chien", "Dog"), dog.Label);
        Assert.All(species, s => Assert.False(string.IsNullOrWhiteSpace(s.Label.Fr) || string.IsNullOrWhiteSpace(s.Label.En)));
    }

    [Fact]
    public async Task Intake_reasons_are_the_seeded_system_list_in_both_languages()
    {
        var reasons = await GetAsync("/api/movements/intake-reasons", tenantId: null);

        Assert.Equal("stray", reasons[0].Code);
        Assert.Equal(new LabelDto("Animal errant", "Stray"), reasons[0].Label);
        Assert.Equal("other", reasons[^1].Code);
    }

    [Fact]
    public async Task Tenant_overrides_apply_to_that_tenant_only()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await InsertOverrideAsync(tenantA, "animals.species_override", "dog", "Chien (adulte)", "Dog (adult)", 10, isHidden: false);
        await InsertOverrideAsync(tenantA, "animals.species_override", "ferret", "Furet", "Ferret", 40, isHidden: true);
        await InsertOverrideAsync(tenantA, "animals.species_override", "goat", "Chèvre", "Goat", 65, isHidden: false);

        var a = await GetAsync("/api/animals/species", tenantA);
        var b = await GetAsync("/api/animals/species", tenantB);
        var none = await GetAsync("/api/animals/species", tenantId: null);

        Assert.Equal(["dog", "cat", "rabbit", "small_mammal", "bird", "goat", "reptile", "other"], a.Select(s => s.Code));
        Assert.Equal(new LabelDto("Chien (adulte)", "Dog (adult)"), a[0].Label);
        Assert.Equal(new LabelDto("Chèvre", "Goat"), a.Single(s => s.Code == "goat").Label);
        Assert.Equal(SystemSpecies, b.Select(s => s.Code));
        Assert.Equal(new LabelDto("Chien", "Dog"), b[0].Label);
        Assert.Equal(SystemSpecies, none.Select(s => s.Code));
    }

    [Fact]
    public async Task Intake_reason_overrides_apply_to_that_tenant_only()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        await InsertOverrideAsync(tenantA, "movements.intake_reason_override", "seizure", "Saisie", "Seizure", 30, isHidden: true);

        var a = await GetAsync("/api/movements/intake-reasons", tenantA);
        var b = await GetAsync("/api/movements/intake-reasons", tenantB);

        Assert.DoesNotContain(a, r => r.Code == "seizure");
        Assert.Contains(b, r => r.Code == "seizure");
    }

    [Theory]
    [InlineData("UPDATE animals.species SET label_fr = 'x' WHERE code = 'dog'")]
    [InlineData("UPDATE movements.intake_reason SET label_fr = 'x' WHERE code = 'stray'")]
    [InlineData("INSERT INTO movements.intake_reason (code, label_fr, label_en, sort_order) VALUES ('x', 'x', 'x', 1)")]
    public async Task Runtime_role_cannot_change_system_values(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(InsufficientPrivilege, error.SqlState);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<List<ItemDto>> GetAsync(string path, Guid? tenantId)
    {
        Guid? user = tenantId is { } tenant ? await TestMemberships.NewMemberAsync(postgres.AppConnectionString, tenant) : null;
        using var client = _factory.CreateHttpsClient(user, tenantId);
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ItemDto>>(TestContext.Current.CancellationToken))!;
    }

    // Table names are test constants, never input.
    private async Task InsertOverrideAsync(Guid tenantId, string table, string code, string fr, string en, int sortOrder, bool isHidden)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            set.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
            await set.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = new NpgsqlCommand(
            $"""
            INSERT INTO {table} (id, tenant_id, code, label_fr, label_en, sort_order, is_hidden)
            VALUES (@id, @tenant, @code, @fr, @en, @sort, @hidden)
            """,
            connection,
            transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
            insert.Parameters.AddWithValue("tenant", tenantId);
            insert.Parameters.AddWithValue("code", code);
            insert.Parameters.AddWithValue("fr", fr);
            insert.Parameters.AddWithValue("en", en);
            insert.Parameters.AddWithValue("sort", sortOrder);
            insert.Parameters.AddWithValue("hidden", isHidden);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record LabelDto(string Fr, string En);

    private sealed record ItemDto(string Code, LabelDto Label);
}
