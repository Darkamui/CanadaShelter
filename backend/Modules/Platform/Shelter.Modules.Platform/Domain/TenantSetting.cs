using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.Modules.Platform.Domain;

/// <summary>
/// A per-organization configuration value (key → value). The reference tenant-owned table: RLS, the tenant query
/// filter and <c>TenantId</c>-leading indexes are proven on it (M1-2, M1-3). No personal data.
/// </summary>
internal sealed class TenantSetting : ITenantOwned
{
    private TenantSetting()
    {
        Key = null!;
        Value = null!;
    }

    public TenantSetting(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Id = Guid.CreateVersion7();
        Key = key;
        Value = value;
    }

    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    public string Key { get; private set; }

    public string Value { get; private set; }

    public void ChangeValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }
}
