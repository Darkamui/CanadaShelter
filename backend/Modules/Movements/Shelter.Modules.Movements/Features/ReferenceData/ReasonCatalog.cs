using Microsoft.EntityFrameworkCore;
using Shelter.BuildingBlocks.Localization;
using Shelter.BuildingBlocks.Persistence;
using Shelter.BuildingBlocks.Tenancy;
using Shelter.Modules.Movements.Domain;

namespace Shelter.Modules.Movements.Features.ReferenceData;

/// <summary>One intake reason or outcome type after the tenant's overrides are applied, hidden ones included.</summary>
internal readonly record struct MergedReason(string Code, LocalizedText Label, int SortOrder, bool IsHidden, string SacCategory);

/// <summary>
/// The intake reason and outcome type lists (ADR 0017 with amendment 1): system values with the current tenant's
/// overrides applied. Hidden values stay in the result so recorded movements keep their label and SAC category; only new
/// choices are limited to <see cref="Visible"/> values.
/// </summary>
internal static class ReasonCatalog
{
    public static async Task<IReadOnlyDictionary<string, MergedReason>> IntakeReasonsAsync(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<IntakeReason>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<IntakeReasonOverride>().AsNoTracking().ToListAsync(cancellationToken);
        return Merge(
            globals.Select(g => new MergedReason(g.Code, g.Label, g.SortOrder, false, g.SacCategory)),
            overrides.Select(o => new MergedReason(o.Code, o.Label, o.SortOrder, o.IsHidden, o.SacCategory)));
    }

    public static async Task<IReadOnlyDictionary<string, MergedReason>> OutcomeTypesAsync(
        ShelterDbContext db, ITenantContext tenant, CancellationToken cancellationToken)
    {
        var globals = await db.Set<OutcomeType>().AsNoTracking().ToListAsync(cancellationToken);
        var overrides = tenant.TenantId is null
            ? []
            : await db.Set<OutcomeTypeOverride>().AsNoTracking().ToListAsync(cancellationToken);
        return Merge(
            globals.Select(g => new MergedReason(g.Code, g.Label, g.SortOrder, false, g.SacCategory)),
            overrides.Select(o => new MergedReason(o.Code, o.Label, o.SortOrder, o.IsHidden, o.SacCategory)));
    }

    /// <summary>The values a user may pick, in list order (sort order, then code).</summary>
    public static IEnumerable<MergedReason> Visible(IReadOnlyDictionary<string, MergedReason> reasons) =>
        reasons.Values
            .Where(r => !r.IsHidden)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code, StringComparer.Ordinal);

    /// <summary>The visible value with this code, if any.</summary>
    public static MergedReason? FindVisible(IReadOnlyDictionary<string, MergedReason> reasons, string? code) =>
        code is not null && reasons.TryGetValue(code, out var reason) && !reason.IsHidden ? reason : null;

    private static Dictionary<string, MergedReason> Merge(IEnumerable<MergedReason> globals, IEnumerable<MergedReason> overrides)
    {
        var merged = globals.ToDictionary(g => g.Code, StringComparer.Ordinal);
        foreach (var o in overrides)
        {
            merged[o.Code] = o;
        }

        return merged;
    }
}
