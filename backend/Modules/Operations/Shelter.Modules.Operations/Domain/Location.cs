using Shelter.BuildingBlocks.Search;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Operations.Domain;

/// <summary>
/// A place in the organization's location tree (shelter → building → room → kennel, or an external clinic). Tenant-owned.
/// The name is plain text and not personal data. Never deleted: archived instead. The tree rules (active parent, unique
/// name among active siblings, no cycle) are checked by <c>LocationTree</c> under a per-tenant lock.
/// </summary>
internal sealed class Location : ITenantOwned
{
    public const int NameMaxLength = 100;
    public const int MaxCapacity = 10_000;

    private Location()
    {
        KindCode = null!;
        Name = null!;
        NormalizedName = null!;
    }

    public Location(Guid? parentId, string kindCode, string name, int? capacity, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        CreatedAt = now;
        Apply(parentId, kindCode, name, capacity, now);
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>The containing location; <see langword="null"/> for a top-level location.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>A code of the merged location kind list.</summary>
    public string KindCode { get; private set; }

    public string Name { get; private set; }

    /// <summary><see cref="SearchNormalizer.Text"/> of <see cref="Name"/>: sibling names are unique on it.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>How many animals the location is meant to hold; <see langword="null"/> when not tracked.</summary>
    public int? Capacity { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(Guid? parentId, string kindCode, string name, int? capacity, DateTimeOffset now) =>
        Apply(parentId, kindCode, name, capacity, now);

    public void Archive(DateTimeOffset now)
    {
        IsArchived = true;
        UpdatedAt = now;
    }

    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(KindCode), nameof(Name), nameof(NormalizedName))]
    private void Apply(Guid? parentId, string kindCode, string name, int? capacity, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kindCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (parentId == Id)
        {
            throw new ArgumentException("A location cannot be its own parent.", nameof(parentId));
        }

        if (capacity is < 0 or > MaxCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        ParentId = parentId;
        KindCode = kindCode;
        Name = name.Trim();
        NormalizedName = SearchNormalizer.Text(Name);
        Capacity = capacity;
        UpdatedAt = now;
    }
}
