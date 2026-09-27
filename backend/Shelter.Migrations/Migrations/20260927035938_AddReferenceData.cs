using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M1-7: bilingual reference data (ADR 0017). Global system lists <c>animals.species</c> and
    /// <c>movements.intake_reason</c>, read-only for the runtime role and seeded here in fr-CA and en-CA; each with a
    /// tenant-owned <c>…_override</c> table under RLS. Later changes to system values need a new migration.
    /// </summary>
    public partial class AddReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "movements");

            migrationBuilder.EnsureSchema(
                name: "animals");

            migrationBuilder.CreateTable(
                name: "intake_reason",
                schema: "movements",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intake_reason", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "intake_reason_override",
                schema: "movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_intake_reason_override", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "species",
                schema: "animals",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_species", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "species_override",
                schema: "animals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_species_override", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intake_reason_override_tenant_id_code",
                schema: "movements",
                table: "intake_reason_override",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_species_override_tenant_id_code",
                schema: "animals",
                table: "species_override",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.GrantRuntimeSchemaUsage("animals");
            migrationBuilder.GrantRuntime("animals", "species", TablePrivileges.Select);
            migrationBuilder.EnableTenantRls("animals", "species_override");
            migrationBuilder.GrantRuntime("animals", "species_override", TablePrivileges.ReadWrite);

            migrationBuilder.GrantRuntimeSchemaUsage("movements");
            migrationBuilder.GrantRuntime("movements", "intake_reason", TablePrivileges.Select);
            migrationBuilder.EnableTenantRls("movements", "intake_reason_override");
            migrationBuilder.GrantRuntime("movements", "intake_reason_override", TablePrivileges.ReadWrite);

            string[] columns = ["code", "label_fr", "label_en", "sort_order"];
            migrationBuilder.InsertData(
                table: "species",
                schema: "animals",
                columns: columns,
                values: new object[,]
                {
                    { "dog", "Chien", "Dog", 10 },
                    { "cat", "Chat", "Cat", 20 },
                    { "rabbit", "Lapin", "Rabbit", 30 },
                    { "ferret", "Furet", "Ferret", 40 },
                    { "small_mammal", "Petit mammifère", "Small mammal", 50 },
                    { "bird", "Oiseau", "Bird", 60 },
                    { "reptile", "Reptile", "Reptile", 70 },
                    { "other", "Autre", "Other", 999 },
                });

            migrationBuilder.InsertData(
                table: "intake_reason",
                schema: "movements",
                columns: columns,
                values: new object[,]
                {
                    { "stray", "Animal errant", "Stray", 10 },
                    // TODO(fr-review): "Abandon par le propriétaire" vs "Remise par le gardien".
                    { "owner_surrender", "Abandon par le propriétaire", "Owner surrender", 20 },
                    { "seizure", "Saisie", "Seizure", 30 },
                    // TODO(fr-review): "Transfert d'un autre organisme".
                    { "transfer_in", "Transfert d'un autre organisme", "Transfer in", 40 },
                    { "returned_adoption", "Retour d'adoption", "Returned adoption", 50 },
                    // TODO(fr-review): "Né sous nos soins" (born in the shelter or in a famille d'accueil).
                    { "born_in_care", "Né sous nos soins", "Born in care", 60 },
                    { "other", "Autre", "Other", 999 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("animals", "species_override");
            migrationBuilder.DisableTenantRls("movements", "intake_reason_override");
            migrationBuilder.RevokeRuntimeSchemaUsage("animals");
            migrationBuilder.RevokeRuntimeSchemaUsage("movements");

            migrationBuilder.DropTable(
                name: "intake_reason",
                schema: "movements");

            migrationBuilder.DropTable(
                name: "intake_reason_override",
                schema: "movements");

            migrationBuilder.DropTable(
                name: "species",
                schema: "animals");

            migrationBuilder.DropTable(
                name: "species_override",
                schema: "animals");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS animals;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS movements;");
        }
    }
}
