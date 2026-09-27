using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M1-5: the append-only audit history <c>audit.audit_event</c>. Tenant-owned with RLS; the runtime role may
    /// only insert and select, the platform-admin role only insert (it audits its own tenant-owned writes).
    /// </summary>
    public partial class AddAuditEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_event",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_tenant_id_entity_type_entity_id_timestamp_utc",
                schema: "audit",
                table: "audit_event",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "timestamp_utc" });

            migrationBuilder.EnableTenantRls("audit", "audit_event");

            migrationBuilder.GrantRuntimeSchemaUsage("audit");
            migrationBuilder.GrantRuntime("audit", "audit_event", TablePrivileges.Select | TablePrivileges.Insert);

            migrationBuilder.GrantPlatformAdminSchemaUsage("audit");
            migrationBuilder.GrantPlatformAdmin("audit", "audit_event", TablePrivileges.Insert);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RevokePlatformAdmin("audit", "audit_event");
            migrationBuilder.RevokePlatformAdminSchemaUsage("audit");
            migrationBuilder.RevokeRuntimeSchemaUsage("audit");

            migrationBuilder.DropTable(
                name: "audit_event",
                schema: "audit");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS audit;");
        }
    }
}
