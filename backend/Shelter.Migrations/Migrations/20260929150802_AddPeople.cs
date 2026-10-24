using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "people");

            migrationBuilder.CreateTable(
                name: "person",
                schema: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    secondary_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    address_line = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    role_tags = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    search_text = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone_digits = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    secondary_phone_digits = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_search_text",
                schema: "people",
                table: "person",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_person_tenant_id_is_archived_display_name_id",
                schema: "people",
                table: "person",
                columns: new[] { "tenant_id", "is_archived", "display_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_person_tenant_id_normalized_email",
                schema: "people",
                table: "person",
                columns: new[] { "tenant_id", "normalized_email" });

            migrationBuilder.CreateIndex(
                name: "ix_person_tenant_id_phone_digits",
                schema: "people",
                table: "person",
                columns: new[] { "tenant_id", "phone_digits" });

            migrationBuilder.CreateIndex(
                name: "ix_person_tenant_id_secondary_phone_digits",
                schema: "people",
                table: "person",
                columns: new[] { "tenant_id", "secondary_phone_digits" });

            // Tenant-owned: every column but the ID, tenant, flag and dates is personal (audited encrypted under the
            // person's key). No DELETE: M3 only archives; erasure comes with the privacy milestone.
            // ix_person_search_text does not lead with tenant_id: a trigram GIN index cannot without btree_gin, which
            // is not installed. RLS and the tenant filter still restrict every query to one organization.
            migrationBuilder.GrantRuntimeSchemaUsage("people");
            migrationBuilder.EnableTenantRls("people", "person");
            migrationBuilder.GrantRuntime("people", "person", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("people", "person");
            migrationBuilder.DropTable(
                name: "person",
                schema: "people");

            migrationBuilder.RevokeRuntimeSchemaUsage("people");
        }
    }
}
