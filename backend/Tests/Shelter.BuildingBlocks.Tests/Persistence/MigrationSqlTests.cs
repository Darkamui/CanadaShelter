using Shelter.BuildingBlocks.Persistence.Migrations;

namespace Shelter.BuildingBlocks.Tests.Persistence;

/// <summary>
/// The SQL the migration helpers emit. Applied migrations call these helpers, so their output must never change
/// meaning: a failure here means an applied migration would now do something else.
/// </summary>
public sealed class MigrationSqlTests
{
    [Fact]
    public void Enable_tenant_rls_forces_rls_with_tenant_and_platform_admin_policies()
    {
        Assert.Equal(
            """
            ALTER TABLE "platform"."tenant_setting" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "platform"."tenant_setting" FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON "platform"."tenant_setting" AS PERMISSIVE FOR ALL TO PUBLIC
                USING (tenant_id = platform.current_tenant_id())
                WITH CHECK (tenant_id = platform.current_tenant_id());
            CREATE POLICY platform_admin_access ON "platform"."tenant_setting" AS PERMISSIVE FOR ALL TO "shelter_platform_admin"
                USING (true)
                WITH CHECK (true);
            """,
            RlsMigrationExtensions.EnableTenantRlsSql("platform", "tenant_setting"),
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void Current_tenant_function_reads_the_transaction_setting_and_fails_closed()
    {
        var sql = RlsMigrationExtensions.CreateCurrentTenantFunctionSql;

        // Missing or empty setting (a pooled connection after SET LOCAL ended) → NULL → no row matches.
        Assert.Contains("NULLIF(current_setting('app.tenant_id', true), '')::uuid", sql, StringComparison.Ordinal);
        Assert.Contains("REVOKE ALL ON FUNCTION platform.current_tenant_id() FROM PUBLIC;", sql, StringComparison.Ordinal);
        Assert.Contains(
            "GRANT EXECUTE ON FUNCTION platform.current_tenant_id() TO \"shelter_app\", \"shelter_platform_admin\";", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TablePrivileges.Select, "SELECT")]
    [InlineData(TablePrivileges.Select | TablePrivileges.Insert, "SELECT, INSERT")]
    [InlineData(TablePrivileges.ReadWrite, "SELECT, INSERT, UPDATE, DELETE")]
    public void Grant_lists_the_requested_privileges(TablePrivileges privileges, string expected)
    {
        Assert.Equal(
            $"GRANT {expected} ON \"platform\".\"organization\" TO \"shelter_app\";",
            GrantMigrationExtensions.GrantSql(DatabaseRoles.App, "platform", "organization", privileges));
    }

    [Fact]
    public void Grant_without_privileges_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => GrantMigrationExtensions.GrantSql(DatabaseRoles.App, "platform", "organization", TablePrivileges.None));
    }

    [Theory]
    [InlineData("Platform")]
    [InlineData("tenant setting")]
    [InlineData("x\"; DROP TABLE y; --")]
    [InlineData("")]
    public void Identifiers_that_are_not_snake_case_are_rejected(string identifier)
    {
        Assert.Throws<ArgumentException>(() => RlsMigrationExtensions.EnableTenantRlsSql("platform", identifier));
    }
}
