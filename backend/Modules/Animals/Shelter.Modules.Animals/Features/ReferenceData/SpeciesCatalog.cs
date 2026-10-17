using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Animals.Domain;

namespace Shelter.Modules.Animals.Features.ReferenceData;

/// <summary>
/// The species list (ADR 0017) with the current tenant's overrides applied, hidden species included, keyed by code.
/// Hidden species keep their label so existing animals still display; new choices must be visible ones.
/// </summary>
internal static class SpeciesCatalog
{
    public static async Task<IReadOnlyDictionary<string, ReferenceEntry>> LoadAsync(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<Species>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<SpeciesOverride>().AsNoTracking().ToListAsync(cancellationToken);

        var merged = globals.ToDictionary(
            s => s.Code, s => new ReferenceEntry(s.Code, s.Label, s.SortOrder), StringComparer.Ordinal);
        foreach (var o in overrides)
        {
            merged[o.Code] = new ReferenceEntry(o.Code, o.Label, o.SortOrder, o.IsHidden);
        }

        return merged;
    }

    /// <summary>Whether <paramref name="code"/> is a species the organization can pick.</summary>
    public static bool IsVisible(IReadOnlyDictionary<string, ReferenceEntry> species, string code) =>
        species.TryGetValue(code, out var entry) && !entry.IsHidden;
}
