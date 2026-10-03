using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M3-4: animal records (<c>animals.animal</c>), their identifiers, the per-organization number counter and the
    /// append-only timeline, all tenant-owned under RLS. No DELETE grants: animals and identifiers are never deleted
    /// (identifiers are deactivated), and the runtime role can only read and insert timeline events. Child foreign keys
    /// include <c>tenant_id</c> so they can never point at another organization's animal (a plain foreign key check
    /// bypasses RLS). The trigram index on <c>search_text</c> cannot start with <c>tenant_id</c> (no <c>btree_gin</c>);
    /// RLS still limits every search to the current organization.
    /// </summary>
    public partial class AddAnimals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "animal",
                schema: "animals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    species_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    secondary_breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    colour = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reproductive_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    birth_date_estimated = table.Column<bool>(type: "boolean", nullable: false),
                    marks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    behaviour_alert = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    medical_alert = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    legal_alert = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    custody_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    in_care_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_outcome_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_animal", x => x.id);
                    table.UniqueConstraint("ak_animal_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_animal_custody", "(custody_status = 'in_care') = (current_location_id IS NOT NULL)");
                    table.CheckConstraint("ck_animal_number", "number > 0");
                });

            migrationBuilder.CreateTable(
                name: "animal_number_counter",
                schema: "animals",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_animal_number_counter", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "animal_identifier",
                schema: "animals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_animal_identifier", x => x.id);
                    table.ForeignKey(
                        name: "fk_animal_identifier_animal_tenant_id_animal_id",
                        columns: x => new { x.tenant_id, x.animal_id },
                        principalSchema: "animals",
                        principalTable: "animal",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "timeline_event",
                schema: "animals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parameters = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_timeline_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_timeline_event_animal_tenant_id_animal_id",
                        columns: x => new { x.tenant_id, x.animal_id },
                        principalSchema: "animals",
                        principalTable: "animal",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_animal_search_text",
                schema: "animals",
                table: "animal",
                column: "search_text")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_animal_tenant_id_custody_status_current_location_id",
                schema: "animals",
                table: "animal",
                columns: new[] { "tenant_id", "custody_status", "current_location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_animal_tenant_id_number",
                schema: "animals",
                table: "animal",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_animal_tenant_id_species_code",
                schema: "animals",
                table: "animal",
                columns: new[] { "tenant_id", "species_code" });

            migrationBuilder.CreateIndex(
                name: "ix_animal_identifier_tenant_id_animal_id",
                schema: "animals",
                table: "animal_identifier",
                columns: new[] { "tenant_id", "animal_id" });

            migrationBuilder.CreateIndex(
                name: "ix_animal_identifier_tenant_id_normalized_value",
                schema: "animals",
                table: "animal_identifier",
                columns: new[] { "tenant_id", "normalized_value" });

            migrationBuilder.CreateIndex(
                name: "ux_animal_identifier_active_microchip",
                schema: "animals",
                table: "animal_identifier",
                columns: new[] { "tenant_id", "normalized_value" },
                unique: true,
                filter: "is_active AND type = 'microchip'");

            migrationBuilder.CreateIndex(
                name: "ix_timeline_event_tenant_id_animal_id_occurred_at_id",
                schema: "animals",
                table: "timeline_event",
                columns: new[] { "tenant_id", "animal_id", "occurred_at", "id" });

            migrationBuilder.EnableTenantRls("animals", "animal");
            migrationBuilder.GrantRuntime("animals", "animal", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
            migrationBuilder.EnableTenantRls("animals", "animal_identifier");
            migrationBuilder.GrantRuntime("animals", "animal_identifier", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
            migrationBuilder.EnableTenantRls("animals", "animal_number_counter");
            migrationBuilder.GrantRuntime("animals", "animal_number_counter", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
            migrationBuilder.EnableTenantRls("animals", "timeline_event");
            migrationBuilder.GrantRuntime("animals", "timeline_event", TablePrivileges.Select | TablePrivileges.Insert);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("animals", "timeline_event");
            migrationBuilder.DisableTenantRls("animals", "animal_number_counter");
            migrationBuilder.DisableTenantRls("animals", "animal_identifier");
            migrationBuilder.DisableTenantRls("animals", "animal");

            migrationBuilder.DropTable(
                name: "animal_identifier",
                schema: "animals");

            migrationBuilder.DropTable(
                name: "animal_number_counter",
                schema: "animals");

            migrationBuilder.DropTable(
                name: "timeline_event",
                schema: "animals");

            migrationBuilder.DropTable(
                name: "animal",
                schema: "animals");
        }
    }
}
