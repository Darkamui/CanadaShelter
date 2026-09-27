using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M2-2: tenant-owned staff memberships (ADR 0018), with tenant RLS plus a self-read policy on
    /// <c>platform.current_user_id()</c> so an account lists its own memberships before choosing an organization.
    /// </summary>
    public partial class AddStaffMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_membership",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_membership", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_staff_membership_tenant_id_user_id",
                schema: "platform",
                table: "staff_membership",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateCurrentUserFunction();
            migrationBuilder.EnableTenantRls("platform", "staff_membership");
            migrationBuilder.EnableSelfRead("platform", "staff_membership");

            // Insert: provisioning and invitations write memberships as the organization. Status changes get UPDATE
            // with the feature that needs them.
            migrationBuilder.GrantRuntime("platform", "staff_membership", TablePrivileges.Select | TablePrivileges.Insert);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_membership",
                schema: "platform");

            // After the table: its self-read policy depends on the function.
            migrationBuilder.DropCurrentUserFunction();
        }
    }
}
