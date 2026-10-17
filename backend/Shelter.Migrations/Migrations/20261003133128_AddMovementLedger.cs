using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M3-5: the append-only movement ledger (<c>movements.movement</c>, tenant-owned under RLS, SELECT and INSERT only
    /// for the runtime role), the outcome type list (global, seeded, plus a tenant override table), and the SAC category
    /// of every intake reason and outcome type (ADR 0017 amendment 1). Animal, location and person IDs point into other
    /// modules, so they have no foreign keys; the void's link to the movement it cancels includes <c>tenant_id</c>.
    /// </summary>
    public partial class AddMovementLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default fills every existing row, override rows included (a column default ignores RLS); it is then
            // dropped, so every new row must give its category. The UpdateData below cannot see override rows (RLS; the
            // migrator does not bypass it), so organizations that added intake reasons before M3-5 keep "other_intake"
            // until they re-categorize them.
            migrationBuilder.AddColumn<string>(
                name: "sac_category",
                schema: "movements",
                table: "intake_reason_override",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "other_intake");

            migrationBuilder.AddColumn<string>(
                name: "sac_category",
                schema: "movements",
                table: "intake_reason",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "other_intake");

            migrationBuilder.Sql("ALTER TABLE movements.intake_reason_override ALTER COLUMN sac_category DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE movements.intake_reason ALTER COLUMN sac_category DROP DEFAULT;");

            // TODO(pilot-review): SAC mapping of the system intake reasons.
            foreach (var (code, category) in new[]
            {
                ("stray", "stray"),
                ("owner_surrender", "relinquished_by_owner"),
                ("seizure", "seized"),
                ("transfer_in", "transfer_in"),
                ("returned_adoption", "relinquished_by_owner"),
                ("born_in_care", "other_intake"),
                ("other", "other_intake"),
            })
            {
                migrationBuilder.UpdateData(
                    table: "intake_reason", schema: "movements", keyColumn: "code", keyValue: code,
                    column: "sac_category", value: category);
            }

            migrationBuilder.CreateTable(
                name: "movement",
                schema: "movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voids_movement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movement", x => x.id);
                    table.UniqueConstraint("ak_movement_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_movement_shape", "(type = 'intake' AND reason_code IS NOT NULL AND to_location_id IS NOT NULL AND from_location_id IS NULL AND voids_movement_id IS NULL)\nOR (type = 'relocation' AND reason_code IS NULL AND from_location_id IS NOT NULL AND to_location_id IS NOT NULL AND from_location_id <> to_location_id AND person_id IS NULL AND voids_movement_id IS NULL)\nOR (type = 'outcome' AND reason_code IS NOT NULL AND from_location_id IS NOT NULL AND to_location_id IS NULL AND voids_movement_id IS NULL)\nOR (type = 'void' AND reason_code IS NULL AND from_location_id IS NULL AND to_location_id IS NULL AND voids_movement_id IS NOT NULL AND notes IS NOT NULL)");
                    table.CheckConstraint("ck_movement_type", "type IN ('intake', 'relocation', 'outcome', 'void')");
                    table.ForeignKey(
                        name: "fk_movement_movement_tenant_id_voids_movement_id",
                        columns: x => new { x.tenant_id, x.voids_movement_id },
                        principalSchema: "movements",
                        principalTable: "movement",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outcome_type",
                schema: "movements",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    sac_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outcome_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "outcome_type_override",
                schema: "movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    sac_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_fr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outcome_type_override", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_movement_tenant_id_animal_id_occurred_at_id",
                schema: "movements",
                table: "movement",
                columns: new[] { "tenant_id", "animal_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_movement_tenant_id_voids_movement_id",
                schema: "movements",
                table: "movement",
                columns: new[] { "tenant_id", "voids_movement_id" },
                unique: true,
                filter: "voids_movement_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outcome_type_override_tenant_id_code",
                schema: "movements",
                table: "outcome_type_override",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            // outcome_type is global reference data (ADR 0017): no tenant_id, no personal data.
            migrationBuilder.GrantRuntime("movements", "outcome_type", TablePrivileges.Select);
            migrationBuilder.EnableTenantRls("movements", "outcome_type_override");
            migrationBuilder.GrantRuntime("movements", "outcome_type_override", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);
            migrationBuilder.EnableTenantRls("movements", "movement");
            migrationBuilder.GrantRuntime("movements", "movement", TablePrivileges.Select | TablePrivileges.Insert);

            // TODO(pilot-review): SAC mapping of the system outcome types.
            migrationBuilder.InsertData(
                table: "outcome_type",
                schema: "movements",
                columns: new[] { "code", "label_fr", "label_en", "sort_order", "sac_category" },
                values: new object[,]
                {
                    { "adoption", "Adoption", "Adoption", 10, "adoption" },
                    // TODO(fr-review): "Retour au propriétaire" vs "Retour au gardien".
                    { "return_to_owner", "Retour au propriétaire", "Return to owner", 20, "return_to_owner" },
                    // TODO(fr-review): "Transfert vers un autre organisme".
                    { "transfer_out", "Transfert vers un autre organisme", "Transfer out", 30, "transfer_out" },
                    // TODO(fr-review): "Retour sur le terrain" (TNR / return to field).
                    { "return_to_field", "Retour sur le terrain", "Return to field", 40, "return_to_field" },
                    { "died", "Décès", "Died", 50, "died_in_care" },
                    { "euthanized", "Euthanasie", "Euthanized", 60, "euthanasia" },
                    { "other", "Autre", "Other", 999, "other_live_outcome" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("movements", "movement");
            migrationBuilder.DisableTenantRls("movements", "outcome_type_override");

            migrationBuilder.DropTable(
                name: "movement",
                schema: "movements");

            migrationBuilder.DropTable(
                name: "outcome_type",
                schema: "movements");

            migrationBuilder.DropTable(
                name: "outcome_type_override",
                schema: "movements");

            migrationBuilder.DropColumn(
                name: "sac_category",
                schema: "movements",
                table: "intake_reason_override");

            migrationBuilder.DropColumn(
                name: "sac_category",
                schema: "movements",
                table: "intake_reason");
        }
    }
}
