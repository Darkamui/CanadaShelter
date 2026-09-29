using Shelter.BuildingBlocks.Authorization;

namespace Shelter.BuildingBlocks.Tests.Authorization;

public sealed class PermissionCatalogTests
{
    private static readonly PermissionCatalog Catalog = new(
    [
        new("thing.read", PermissionKind.Read),
        new("thing.write", PermissionKind.Write),
        new("secret.read", PermissionKind.Read, Sensitive: true),
        new("secret.write", PermissionKind.Write, Sensitive: true),
    ]);

    [Fact]
    public void Administrator_gets_every_permission()
    {
        Assert.Equal(["secret.read", "secret.write", "thing.read", "thing.write"], Catalog.PermissionsFor([SystemRoles.Administrator]).Order());
    }

    [Fact]
    public void Staff_gets_every_non_sensitive_permission()
    {
        Assert.Equal(["thing.read", "thing.write"], Catalog.PermissionsFor([SystemRoles.Staff]).Order());
    }

    [Fact]
    public void Read_only_gets_non_sensitive_reads()
    {
        Assert.Equal(["thing.read"], Catalog.PermissionsFor([SystemRoles.ReadOnly]));
    }

    [Fact]
    public void Roles_combine_and_unknown_or_missing_roles_grant_nothing()
    {
        Assert.Equal(["thing.read", "thing.write"], Catalog.PermissionsFor([SystemRoles.ReadOnly, SystemRoles.Staff]).Order());
        Assert.Empty(Catalog.PermissionsFor(["owner"]));
        Assert.Empty(Catalog.PermissionsFor([]));
    }

    [Fact]
    public void Duplicate_permission_names_fail()
    {
        Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(
        [
            new("thing.read", PermissionKind.Read),
            new("thing.read", PermissionKind.Write),
        ]));
    }
}
