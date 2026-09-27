using Microsoft.EntityFrameworkCore.Migrations;

namespace Shelter.BuildingBlocks.Persistence.Migrations;

/// <summary>Table privileges a role may be granted.</summary>
[Flags]
public enum TablePrivileges
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
/// Grants to the runtime and platform-admin roles, so migrations state each role's access explicitly and
/// reviewably instead of hand-writing SQL. Output must never change meaning: applied migrations call these.
/// </summary>
public static class GrantMigrationExtensions
{
    /// <summary><c>GRANT USAGE ON SCHEMA</c> to the runtime role.</summary>
    public static MigrationBuilder GrantRuntimeSchemaUsage(this MigrationBuilder migrationBuilder, string schema) =>
        migrationBuilder.GrantSchemaUsage(DatabaseRoles.App, schema);

    /// <summary>Reverses <see cref="GrantRuntimeSchemaUsage"/> (EF never drops schemas, so Down must revoke).</summary>
    public static MigrationBuilder RevokeRuntimeSchemaUsage(this MigrationBuilder migrationBuilder, string schema) =>
        migrationBuilder.RevokeSchemaUsage(DatabaseRoles.App, schema);

    /// <summary>
    /// <c>GRANT USAGE, CREATE ON SCHEMA</c> to the runtime role, for a library that installs and upgrades its own
    /// non-tenant tables in a dedicated schema (Hangfire). Never for a module schema.
    /// </summary>
    public static MigrationBuilder GrantRuntimeSchemaCreate(this MigrationBuilder migrationBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"GRANT USAGE, CREATE ON SCHEMA {SqlIdentifier.Quote(schema)} TO {SqlIdentifier.Quote(DatabaseRoles.App)};");
        return migrationBuilder;
    }

    /// <summary>Grants exactly <paramref name="privileges"/> on one table to the runtime role.</summary>
    public static MigrationBuilder GrantRuntime(this MigrationBuilder migrationBuilder, string schema, string table, TablePrivileges privileges)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(GrantSql(DatabaseRoles.App, schema, table, privileges));
        return migrationBuilder;
    }

    /// <summary><c>GRANT USAGE ON SCHEMA</c> to the platform-admin role.</summary>
    public static MigrationBuilder GrantPlatformAdminSchemaUsage(this MigrationBuilder migrationBuilder, string schema) =>
        migrationBuilder.GrantSchemaUsage(DatabaseRoles.PlatformAdmin, schema);

    /// <summary>Reverses <see cref="GrantPlatformAdminSchemaUsage"/>.</summary>
    public static MigrationBuilder RevokePlatformAdminSchemaUsage(this MigrationBuilder migrationBuilder, string schema) =>
        migrationBuilder.RevokeSchemaUsage(DatabaseRoles.PlatformAdmin, schema);

    /// <summary>Grants exactly <paramref name="privileges"/> on one table to the platform-admin role.</summary>
    public static MigrationBuilder GrantPlatformAdmin(this MigrationBuilder migrationBuilder, string schema, string table, TablePrivileges privileges)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(GrantSql(DatabaseRoles.PlatformAdmin, schema, table, privileges));
        return migrationBuilder;
    }

    /// <summary>Revokes every table privilege of the platform-admin role on one table (for Down).</summary>
    public static MigrationBuilder RevokePlatformAdmin(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"REVOKE ALL ON {SqlIdentifier.Qualified(schema, table)} FROM {SqlIdentifier.Quote(DatabaseRoles.PlatformAdmin)};");
        return migrationBuilder;
    }

    /// <summary>The <c>GRANT</c> statement for <paramref name="role"/>.</summary>
    public static string GrantSql(string role, string schema, string table, TablePrivileges privileges)
    {
        var list = PrivilegeList(privileges);
        if (list.Count == 0)
        {
            throw new ArgumentException("At least one privilege is required.", nameof(privileges));
        }

        return $"GRANT {string.Join(", ", list)} ON {SqlIdentifier.Qualified(schema, table)} TO {SqlIdentifier.Quote(role)};";
    }

    private static MigrationBuilder GrantSchemaUsage(this MigrationBuilder migrationBuilder, string role, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"GRANT USAGE ON SCHEMA {SqlIdentifier.Quote(schema)} TO {SqlIdentifier.Quote(role)};");
        return migrationBuilder;
    }

    private static MigrationBuilder RevokeSchemaUsage(this MigrationBuilder migrationBuilder, string role, string schema)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"REVOKE USAGE ON SCHEMA {SqlIdentifier.Quote(schema)} FROM {SqlIdentifier.Quote(role)};");
        return migrationBuilder;
    }

    private static List<string> PrivilegeList(TablePrivileges privileges)
    {
        var list = new List<string>();
        if (privileges.HasFlag(TablePrivileges.Select))
        {
            list.Add("SELECT");
        }

        if (privileges.HasFlag(TablePrivileges.Insert))
        {
            list.Add("INSERT");
        }

        if (privileges.HasFlag(TablePrivileges.Update))
        {
            list.Add("UPDATE");
        }

        if (privileges.HasFlag(TablePrivileges.Delete))
        {
            list.Add("DELETE");
        }

        return list;
    }
}
