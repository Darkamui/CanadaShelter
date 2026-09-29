using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitation_token",
                schema: "platform",
                columns: table => new
                {
                    token_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_token", x => x.token_hash);
                });

            migrationBuilder.CreateTable(
                name: "staff_invitation",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role_keys = table.Column<string[]>(type: "text[]", nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_invitation", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_token_invitation_id",
                schema: "platform",
                table: "invitation_token",
                column: "invitation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitation_tenant_id_created_at",
                schema: "platform",
                table: "staff_invitation",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitation_tenant_id_normalized_email",
                schema: "platform",
                table: "staff_invitation",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true,
                filter: "accepted_at IS NULL AND revoked_at IS NULL");

            // Tenant-owned: invitations are created, resent, revoked and accepted as their organization (the accept
            // in a scope whose tenant comes from the token). No DELETE: closed invitations stay for history.
            migrationBuilder.EnableTenantRls("platform", "staff_invitation");
            migrationBuilder.GrantRuntime("platform", "staff_invitation", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Update);

            // Intentionally global, no RLS (ADR 0018): the anonymous accept knows only the secret, and must find the
            // organization from it before any tenant is set. No personal data: SHA-256 hash, two IDs, a date. Rows
            // are deleted when the invitation is accepted, revoked or resent.
            migrationBuilder.GrantRuntime("platform", "invitation_token", TablePrivileges.Select | TablePrivileges.Insert | TablePrivileges.Delete);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_token",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "staff_invitation",
                schema: "platform");
        }
    }
}
