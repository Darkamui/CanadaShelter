using Microsoft.EntityFrameworkCore.Migrations;

namespace Shelter.BuildingBlocks.Persistence.Migrations;

/// <summary>Table privileges the runtime role may be granted.</summary>
[Flags]
public enum RuntimePrivileges
{
    /// <summary>No privilege.</summary>
    None = 0,

    /// <summary><c>SELECT</c>.</summary>
    Select = 1,

    /// <summary><c>INSERT</c>.</summary>
    Insert = 2,

    /// <summary><c>UPDATE</c>.</summary>
    Update = 4,

    /// <summary><c>DELETE</c>.</summary>
    Delete = 8,

    /// <summary>Ordinary read/write table: <c>SELECT, INSERT, UPDATE, DELETE</c>.</summary>
    ReadWrite = Select | Insert | Update | Delete,
}

/// <summary>
/// Grants to <see cref="DatabaseRoles.App"/>, so migrations state the runtime role's access explicitly and
/// reviewably instead of hand-writing SQL. Output must never change meaning: applied migrations call these.
/// </summary>
public static class GrantMigrationExtensions
{
    /// <summary><c>GRANT USAGE ON SCHEMA</c> to the runtime role.</summary>
    public static MigrationBuilder GrantRuntimeSchemaUsage(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"GRANT USAGE ON SCHEMA {SqlIdentifier.Quote(schema)} TO {DatabaseRoles.App};");
        return migrationBuilder;
    }

    /// <summary>Reverses <see cref="GrantRuntimeSchemaUsage"/> (EF never drops schemas, so Down must revoke).</summary>
    public static MigrationBuilder RevokeRuntimeSchemaUsage(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"REVOKE USAGE ON SCHEMA {SqlIdentifier.Quote(schema)} FROM {DatabaseRoles.App};");
        return migrationBuilder;
    }

    /// <summary>Grants exactly <paramref name="privileges"/> on one table to the runtime role.</summary>
    public static MigrationBuilder GrantRuntime(this MigrationBuilder migrationBuilder, string schema, string table, RuntimePrivileges privileges)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(GrantSql(schema, table, privileges));
        return migrationBuilder;
    }

    /// <summary>The SQL emitted by <see cref="GrantRuntime"/>.</summary>
    public static string GrantSql(string schema, string table, RuntimePrivileges privileges)
    {
        var list = PrivilegeList(privileges);
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one privilege is required.", nameof(privileges));
        }

        return $"GRANT {string.Join(", ", list)} ON {SqlIdentifier.Qualified(schema, table)} TO {DatabaseRoles.App};";
    }

    private static List<string> PrivilegeList(RuntimePrivileges privileges)
    {
        var list = new List<string>();
        if (privileges.HasFlag(RuntimePrivileges.Select))
        {
            list.Add("SELECT");
        }

        if (privileges.HasFlag(RuntimePrivileges.Insert))
        {
            list.Add("INSERT");
        }

        if (privileges.HasFlag(RuntimePrivileges.Update))
        {
            list.Add("UPDATE");
        }

        if (privileges.HasFlag(RuntimePrivileges.Delete))
        {
            list.Add("DELETE");
        }

        return list;
    }
}
