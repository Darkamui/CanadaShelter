using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Auditing;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Persistence.Migrations;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Testing.Auditing;

/// <summary>
/// Test-only tenant-owned entity with one field of each classification, for audit and crypto-shredding tests.
/// Its table <c>testing.sample_person</c> is not in the migrations: create it with <see cref="CreateTableSql"/>
/// as the schema owner, then compose <see cref="SamplePersonModelContributor"/>.
/// </summary>
public sealed class SamplePerson : ITenantOwned
{
    public const string Schema = "testing";
    public const string Table = "sample_person";

    public SamplePerson(string email, string nickname, string code)
    {
        Id = Guid.CreateVersion7();
        Email = email;
        Nickname = nickname;
        Code = code;
    }

    private SamplePerson()
    {
        Email = null!;
        Nickname = null!;
        Code = null!;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Personal.</summary>
    public string Email { get; private set; }

    /// <summary>Deliberately unclassified.</summary>
    public string Nickname { get; private set; }

    /// <summary>Non-personal.</summary>
    public string Code { get; private set; }

    public void Change(string email, string nickname, string code)
    {
        Email = email;
        Nickname = nickname;
        Code = code;
    }

    /// <summary>
    /// Creates <c>testing.sample_person</c> with RLS and runtime grants, as a module migration would. Run once, as
    /// the schema owner.
    /// </summary>
    public static string CreateTableSql =>
        $"""
        CREATE SCHEMA {Schema};
        CREATE TABLE {Schema}.{Table} (
            id uuid PRIMARY KEY,
            tenant_id uuid NOT NULL,
            email text NOT NULL,
            nickname text NOT NULL,
            code text NOT NULL);
        CREATE INDEX ix_{Table}_tenant_id ON {Schema}.{Table} (tenant_id);
        {RlsMigrationExtensions.EnableTenantRlsSql(Schema, Table)}
        GRANT USAGE ON SCHEMA {Schema} TO {DatabaseRoles.App};
        {GrantMigrationExtensions.GrantSql(DatabaseRoles.App, Schema, Table, TablePrivileges.ReadWrite)}
        """;
}

/// <summary>Maps <see cref="SamplePerson"/>.</summary>
public sealed class SamplePersonModelContributor : IModelContributor
{
    /// <inheritdoc />
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var builder = modelBuilder.Entity<SamplePerson>();
        builder.ToTable(SamplePerson.Table, SamplePerson.Schema);
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Email).IsRequired().IsPersonalData();
        builder.Property(p => p.Nickname).IsRequired();
        builder.Property(p => p.Code).IsRequired().IsNonPersonalData();
    }
}
