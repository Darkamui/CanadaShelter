using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M2-3: system role keys on each staff membership (ADR 0018). Existing memberships get none (no permissions);
    /// the dev seed restores the demo administrator. The runtime role may update only roles and status, which also
    /// lets it lock administrator rows (<c>FOR UPDATE</c>) for the last-administrator guard.
    /// </summary>
    public partial class AddMembershipRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "role_keys",
                schema: "platform",
                table: "staff_membership",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.GrantRuntimeColumnUpdate("platform", "staff_membership", "role_keys", "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE UPDATE ON platform.staff_membership FROM shelter_app;");

            migrationBuilder.DropColumn(
                name: "role_keys",
                schema: "platform",
                table: "staff_membership");
        }
    }
}
