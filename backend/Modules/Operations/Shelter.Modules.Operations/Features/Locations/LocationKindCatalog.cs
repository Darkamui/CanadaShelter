using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Operations.Domain;

namespace Shelter.Modules.Operations.Features.Locations;

/// <summary>One location kind after the tenant's overrides are applied, hidden ones included.</summary>
internal readonly record struct MergedLocationKind(string Code, LocalizedText Label, int SortOrder, bool IsHidden, bool HoldsAnimals);

/// <summary>
/// The location kind list (ADR 0017 with the attribute amendment): system kinds with the current tenant's overrides
/// applied. Hidden kinds stay in the result so existing locations keep their label and <c>HoldsAnimals</c>; only new
/// choices are limited to <see cref="Visible"/> kinds.
/// </summary>
internal static class LocationKindCatalog
{
    public static async Task<IReadOnlyDictionary<string, MergedLocationKind>> LoadAsync(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<LocationKind>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<LocationKindOverride>().AsNoTracking().ToListAsync(cancellationToken);

        var merged = globals.ToDictionary(
            k => k.Code,
            k => new MergedLocationKind(k.Code, k.Label, k.SortOrder, false, k.HoldsAnimals),
            StringComparer.Ordinal);
        foreach (var o in overrides)
        {
            merged[o.Code] = new MergedLocationKind(o.Code, o.Label, o.SortOrder, o.IsHidden, o.HoldsAnimals);
        }

        return merged;
    }

    /// <summary>The kinds a user may pick, in list order (sort order, then code).</summary>
    public static IEnumerable<MergedLocationKind> Visible(IReadOnlyDictionary<string, MergedLocationKind> kinds) =>
        kinds.Values
            .Where(k => !k.IsHidden)
            .OrderBy(k => k.SortOrder)
            .ThenBy(k => k.Code, StringComparer.Ordinal);
}
