using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M3-3: the location tree (<c>operations.location</c>, tenant-owned under RLS) and the bilingual location kind list
    /// (ADR 0017 with its attribute amendment): global <c>operations.location_kind</c>, read-only for the runtime role and
    /// seeded here, plus the tenant-owned <c>location_kind_override</c>. No DELETE grants: locations are archived, never
    /// deleted. The parent foreign key includes <c>tenant_id</c> so a parent can never be another organization's row
    /// (a plain foreign key check bypasses RLS).
    /// </summary>
    public partial class AddLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "location",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location", x => x.id);
                    table.UniqueConstraint("ak_location_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_location_capacity", "capacity IS NULL OR capacity BETWEEN 0 AND 10000");
                    table.ForeignKey(
                        name: "fk_location_location_tenant_id_parent_id",
                        columns: x => new { x.tenant_id, x.parent_id },
                        principalSchema: "operations",
                        principalTable: "location",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_kind",
                schema: "operations",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    holds_animals = table.Column<bool>(type: "boolean", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_kind", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "location_kind_override",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    holds_animals = table.Column<bool>(type: "boolean", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_kind_override", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_location_tenant_id_parent_id",
                schema: "operations",
                table: "location",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_location_tenant_id_parent_id_normalized_name",
                schema: "operations",
                table: "location",
                columns: new[] { "tenant_id", "parent_id", "normalized_name" },
                unique: true,
                filter: "NOT is_archived")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_location_kind_override_tenant_id_code",
                schema: "operations",
                table: "location_kind_override",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.GrantRuntimeSchemaUsage("operations");
            migrationBuilder.GrantRuntime("operations", "location_kind", TablePrivileges.Select);
            migrationBuilder.EnableTenantRls("operations", "location_kind_override");
            migrationBuilder.GrantRuntime("operations", "location_kind_override", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
            migrationBuilder.EnableTenantRls("operations", "location");
            migrationBuilder.GrantRuntime("operations", "location", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);

            migrationBuilder.InsertData(
                table: "location_kind",
                schema: "operations",
                columns: ["code", "label_fr", "label_en", "sort_order", "holds_animals"],
                values: new object[,]
                {
                    { "shelter", "Refuge", "Shelter", 10, true },
                    { "building", "Bâtiment", "Building", 20, false },
                    { "room", "Salle", "Room", 30, true },
                    // TODO(fr-review): "Enclos" vs "Chenil" for one dog's kennel.
                    { "kennel", "Enclos", "Kennel", 40, true },
                    { "cage", "Cage", "Cage", 50, true },
                    { "clinic", "Clinique", "Clinic", 60, true },
                    // TODO(fr-review): "Isolement" (isolation ward).
                    { "isolation", "Isolement", "Isolation", 70, true },
                    // TODO(fr-review): "Clinique vétérinaire externe".
                    { "external_clinic", "Clinique vétérinaire externe", "External clinic", 80, true },
                    { "partner_shelter", "Refuge partenaire", "Partner shelter", 90, true },
                    // TODO(fr-review): "Terrain" (in the field, e.g. a community cat colony).
                    { "field", "Terrain", "Field", 100, true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("operations", "location");
            migrationBuilder.DisableTenantRls("operations", "location_kind_override");

            migrationBuilder.DropTable(
                name: "location",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "location_kind",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "location_kind_override",
                schema: "operations");

            migrationBuilder.RevokeRuntimeSchemaUsage("operations");
        }
    }
}
