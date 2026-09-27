using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M1-2: <c>platform.current_tenant_id()</c>, RLS on <c>platform.tenant_setting</c>, and the platform-admin
    /// role's access (provisioning organizations, cross-tenant reads). No model change.
    /// </summary>
    public partial class AddTenantRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateCurrentTenantFunction();
            migrationBuilder.EnableTenantRls("platform", "tenant_setting");

            migrationBuilder.GrantPlatformAdminSchemaUsage("platform");
            migrationBuilder.GrantPlatformAdmin("platform", "organization", TablePrivileges.Select | TablePrivileges.Insert);
            migrationBuilder.GrantPlatformAdmin("platform", "tenant_setting", TablePrivileges.Select);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RevokePlatformAdmin("platform", "tenant_setting");
            migrationBuilder.RevokePlatformAdmin("platform", "organization");
            migrationBuilder.RevokePlatformAdminSchemaUsage("platform");

            migrationBuilder.DisableTenantRls("platform", "tenant_setting");
            migrationBuilder.DropCurrentTenantFunction();
        }
    }
}
