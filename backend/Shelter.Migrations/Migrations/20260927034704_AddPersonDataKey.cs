using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M1-6: crypto-shredding. <c>audit.person_data_key</c> holds one wrapped data key per tenant and subject;
    /// tenant-owned with RLS. The runtime role may read and insert keys and update only <c>wrapped_key</c> and
    /// <c>shredded_at</c> (to shred); never delete, so a tombstone keeps a shredded subject from getting a new key.
    /// <c>audit_event.subject_id</c> names the key a row's personal values are encrypted with.
    /// </summary>
    public partial class AddPersonDataKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                schema: "audit",
                table: "audit_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "person_data_key",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wrapped_key = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    shredded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_data_key", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_tenant_id_subject_id",
                schema: "audit",
                table: "audit_event",
                columns: new[] { "tenant_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_person_data_key_tenant_id_subject_id",
                schema: "audit",
                table: "person_data_key",
                columns: new[] { "tenant_id", "subject_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("audit", "person_data_key");
            migrationBuilder.GrantRuntime("audit", "person_data_key", TablePrivileges.Select | TablePrivileges.Insert);
            migrationBuilder.GrantRuntimeColumnUpdate("audit", "person_data_key", "wrapped_key", "shredded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("audit", "person_data_key");

            migrationBuilder.DropTable(
                name: "person_data_key",
                schema: "audit");

            migrationBuilder.DropIndex(
                name: "ix_audit_event_tenant_id_subject_id",
                schema: "audit",
                table: "audit_event");

            migrationBuilder.DropColumn(
                name: "subject_id",
                schema: "audit",
                table: "audit_event");
        }
    }
}
