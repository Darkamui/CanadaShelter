using Npgsql;

namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>M1-3: integration tests connect as the runtime role, which cannot bypass RLS.</summary>
public sealed class RuntimeRoleTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Tests_connect_as_shelter_app_without_rls_bypass()
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT current_user::text, rolsuper, rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));

        Assert.Equal("shelter_app", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
    }
}
