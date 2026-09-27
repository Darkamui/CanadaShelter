namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// An organization: the SaaS tenant (architecture §8.1). A global row (it is the tenant, so it has no
/// <c>TenantId</c>) and holds no personal data: names and slugs identify organizations, not people.
/// </summary>
internal sealed class Organization
{
    private Organization()
    {
        Name = null!;
        Slug = null!;
    }

    public Organization(string name, string slug, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        Id = Guid.CreateVersion7();
        Name = name;
        Slug = slug;
        Status = OrganizationStatus.Active;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>URL-safe unique handle (lowercase letters, digits, hyphens).</summary>
    public string Slug { get; private set; }

    public OrganizationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

internal enum OrganizationStatus
{
    Active,
    Suspended,
}
