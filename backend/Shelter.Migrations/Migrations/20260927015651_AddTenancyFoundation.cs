using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "organization",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_setting",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_setting", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_organization_slug",
                schema: "platform",
                table: "organization",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_setting_tenant_id_key",
                schema: "platform",
                table: "tenant_setting",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            // Runtime role: read the tenant registry, read/write tenant settings. Organization writes go
            // through the platform-admin path (M1-2), never shelter_app.
            migrationBuilder.GrantRuntimeSchemaUsage("platform");
            migrationBuilder.GrantRuntime("platform", "organization", TablePrivileges.Select);
            migrationBuilder.GrantRuntime("platform", "tenant_setting", TablePrivileges.ReadWrite);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RevokeRuntimeSchemaUsage("platform");

            migrationBuilder.DropTable(
                name: "organization",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_setting",
                schema: "platform");
        }
    }
}
