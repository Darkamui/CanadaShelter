using Microsoft.EntityFrameworkCore.Migrations;
using Shelter.BuildingBlocks.Persistence.Migrations;

#nullable disable

namespace Shelter.Migrations.Migrations
{
    /// <summary>
    /// M1-4: the <c>hangfire</c> schema (ADR 0010). Hangfire installs and upgrades its own tables in it as the
    /// runtime role; none of them is tenant-owned (job arguments carry IDs only). No model change.
    /// </summary>
    public partial class AddHangfireSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema("hangfire");
            migrationBuilder.GrantRuntimeSchemaCreate("hangfire");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hangfire's tables are owned by the runtime role; the schema owner may still drop them with the schema.
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS hangfire CASCADE;");
        }
    }
}
