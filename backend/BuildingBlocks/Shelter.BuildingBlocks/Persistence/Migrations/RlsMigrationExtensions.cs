using Microsoft.EntityFrameworkCore.Migrations;

namespace Shelter.BuildingBlocks.Persistence.Migrations;

/// <summary>
/// Row-level security for tenant-owned tables (ADR 0003, 0004). Every tenant-owned table calls
/// <see cref="EnableTenantRls"/> in the migration that creates it. Output must never change meaning: applied
/// migrations call these.
/// </summary>
public static class RlsMigrationExtensions
{
    /// <summary>Transaction-scoped setting carrying the current tenant (set with <c>SET LOCAL</c>).</summary>
    public const string TenantSetting = "app.tenant_id";

    /// <summary>Policy restricting every role to the current tenant's rows.</summary>
    public const string TenantPolicy = "tenant_isolation";

    /// <summary>Policy letting the platform-admin role reach every tenant's rows (it still needs table grants).</summary>
    public const string PlatformAdminPolicy = "platform_admin_access";

    /// <summary>
    /// <c>platform.current_tenant_id()</c>: the tenant of the current transaction, or <c>NULL</c> when unset. A pooled
    /// connection reports <c>''</c> after a transaction that used <c>SET LOCAL</c>; that is <c>NULL</c> too.
    /// </summary>
    public const string CurrentTenantFunction = "platform.current_tenant_id()";

    /// <summary>Creates <see cref="CurrentTenantFunction"/>; call once, before the first <see cref="EnableTenantRls"/>.</summary>
    public static MigrationBuilder CreateCurrentTenantFunction(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(CreateCurrentTenantFunctionSql);
        return migrationBuilder;
    }

    /// <summary>Reverses <see cref="CreateCurrentTenantFunction"/>.</summary>
    public static MigrationBuilder DropCurrentTenantFunction(this MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql($"DROP FUNCTION {CurrentTenantFunction};");
        return migrationBuilder;
    }

    /// <summary>
    /// Enables and forces RLS on a table with a <c>tenant_id</c> column and creates its policies: rows are visible
    /// and writable only when <c>tenant_id</c> equals the transaction's tenant. No tenant set → no rows (fail
    /// closed). <c>FORCE</c> applies the policies to the table owner as well.
    /// Call it in the same migration that creates the table, before any grant to the runtime role, so the table
    /// never exists without RLS. (<c>platform.tenant_setting</c> predates this helper and got RLS one migration
    /// later, in the same release.)
    /// </summary>
    public static MigrationBuilder EnableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(EnableTenantRlsSql(schema, table));
        return migrationBuilder;
    }

    /// <summary>Reverses <see cref="EnableTenantRls"/>.</summary>
    public static MigrationBuilder DisableTenantRls(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(DisableTenantRlsSql(schema, table));
        return migrationBuilder;
    }

    /// <summary>The SQL emitted by <see cref="CreateCurrentTenantFunction"/>.</summary>
    public static string CreateCurrentTenantFunctionSql =>
        $"""
        CREATE FUNCTION {CurrentTenantFunction} RETURNS uuid
            LANGUAGE sql STABLE PARALLEL SAFE
            AS $$ SELECT NULLIF(current_setting('{TenantSetting}', true), '')::uuid $$;
        REVOKE ALL ON FUNCTION {CurrentTenantFunction} FROM PUBLIC;
        GRANT EXECUTE ON FUNCTION {CurrentTenantFunction} TO {SqlIdentifier.Quote(DatabaseRoles.App)}, {SqlIdentifier.Quote(DatabaseRoles.PlatformAdmin)};
        """;

    /// <summary>The SQL emitted by <see cref="EnableTenantRls"/>.</summary>
    public static string EnableTenantRlsSql(string schema, string table)
    {
        var name = SqlIdentifier.Qualified(schema, table);
        return $"""
            ALTER TABLE {name} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {name} FORCE ROW LEVEL SECURITY;
            CREATE POLICY {TenantPolicy} ON {name} AS PERMISSIVE FOR ALL TO PUBLIC
                USING (tenant_id = {CurrentTenantFunction})
                WITH CHECK (tenant_id = {CurrentTenantFunction});
            CREATE POLICY {PlatformAdminPolicy} ON {name} AS PERMISSIVE FOR ALL TO {SqlIdentifier.Quote(DatabaseRoles.PlatformAdmin)}
                USING (true)
                WITH CHECK (true);
            """;
    }

    /// <summary>The SQL emitted by <see cref="DisableTenantRls"/>.</summary>
    public static string DisableTenantRlsSql(string schema, string table)
    {
        var name = SqlIdentifier.Qualified(schema, table);
        return $"""
            DROP POLICY {PlatformAdminPolicy} ON {name};
            DROP POLICY {TenantPolicy} ON {name};
            ALTER TABLE {name} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {name} DISABLE ROW LEVEL SECURITY;
            """;
    }
}
