namespace Shelter.BuildingBlocks.Localization;

/// <summary>One value of a reference list as the API returns it: a stable code and both labels.</summary>
/// <param name="Code">Stable identifier stored by referencing rows; never shown to users.</param>
/// <param name="Label">Label in both languages.</param>
public sealed record ReferenceItem(string Code, LocalizedText Label);

/// <summary>A reference-list row, global or tenant override, as read for merging.</summary>
/// <param name="Code">Stable code.</param>
/// <param name="Label">Label in both languages.</param>
/// <param name="SortOrder">Position in the list; ties sort by code.</param>
/// <param name="IsHidden">Override only: hides the global value with the same code for this tenant.</param>
public readonly record struct ReferenceEntry(string Code, LocalizedText Label, int SortOrder, bool IsHidden = false);

/// <summary>
/// The reference-data pattern (ADR 0017): each list has a global table of system values, seeded by migrations,
/// and a tenant-owned <c>…_override</c> table. A tenant override with a global code relabels, reorders or hides
/// it; one with a new code adds a tenant value.
/// </summary>
public static class ReferenceList
{
    /// <summary>The list one tenant sees: globals with that tenant's overrides applied, hidden values removed.</summary>
    public static IReadOnlyList<ReferenceItem> Merge(IEnumerable<ReferenceEntry> globals, IEnumerable<ReferenceEntry> overrides)
    {
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(overrides);

        var entries = globals.ToDictionary(g => g.Code, StringComparer.Ordinal);
        foreach (var entry in overrides)
        {
            entries[entry.Code] = entry;
        }

        return [.. entries.Values
            .Where(e => !e.IsHidden)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Code, StringComparer.Ordinal)
            .Select(e => new ReferenceItem(e.Code, e.Label))];
    }
}
