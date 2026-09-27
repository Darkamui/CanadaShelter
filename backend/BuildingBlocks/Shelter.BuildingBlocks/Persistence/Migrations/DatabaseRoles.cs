namespace Shelter.BuildingBlocks.Persistence.Migrations;

/// <summary>PostgreSQL roles created by <c>infrastructure/docker/postgres/init/01-roles.sh</c> (ADR 0003).</summary>
public static class DatabaseRoles
{
    /// <summary>Owns schemas and tables; runs migrations only.</summary>
    public const string Migrator = "shelter_migrator";

    /// <summary>Runtime role: <c>NOBYPASSRLS</c>, owns nothing, gets only the grants migrations give it.</summary>
    public const string App = "shelter_app";
}
