using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

namespace Shelter.BuildingBlocks.Authorization;

/// <summary>Whether a permission only reads or also changes data; <see cref="SystemRoles.ReadOnly"/> gets reads only.</summary>
public enum PermissionKind
{
    /// <summary>Reads data.</summary>
    Read,

    /// <summary>Changes data.</summary>
    Write,
}

/// <summary>
/// One permission, declared by the module that owns it in its <c>Authorization/</c> folder (ADR 0018). Sensitive
/// permissions (audit, staff management, personal data exports) belong to <see cref="SystemRoles.Administrator"/> only.
/// </summary>
/// <param name="Name">Dotted, lower-case name, such as <c>animal.read</c>. Stable: stored nowhere, but used by clients.</param>
/// <param name="Kind">Read or write.</param>
/// <param name="Sensitive">Whether only administrators get it.</param>
public sealed record PermissionDefinition(string Name, PermissionKind Kind, bool Sensitive = false);

/// <summary>The code-defined roles a membership can hold (ADR 0018). Custom roles are a follow-up.</summary>
public static class SystemRoles
{
    /// <summary>Every permission, including ones added later.</summary>
    public const string Administrator = "administrator";

    /// <summary>Every non-sensitive permission.</summary>
    public const string Staff = "staff";

    /// <summary>Every non-sensitive read.</summary>
    public const string ReadOnly = "read_only";

    /// <summary>All system role keys.</summary>
    public static IReadOnlySet<string> All { get; } = FrozenSet.Create(StringComparer.Ordinal, Administrator, Staff, ReadOnly);
}

/// <summary>
/// Every declared permission, and what each system role grants. Singleton, composed from the
/// <see cref="PermissionDefinition"/>s the modules register; duplicate names fail at startup.
/// </summary>
public sealed class PermissionCatalog
{
    private readonly FrozenDictionary<string, FrozenSet<string>> _byRole;

    public PermissionCatalog(IEnumerable<PermissionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var byName = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!byName.TryAdd(definition.Name, definition))
            {
                throw new InvalidOperationException($"Permission '{definition.Name}' is declared twice.");
            }
        }

        Definitions = byName.Values.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        _byRole = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [SystemRoles.Administrator] = Names(_ => true),
            [SystemRoles.Staff] = Names(d => !d.Sensitive),
            [SystemRoles.ReadOnly] = Names(d => !d.Sensitive && d.Kind == PermissionKind.Read),
        }.ToFrozenDictionary(StringComparer.Ordinal);

        FrozenSet<string> Names(Func<PermissionDefinition, bool> predicate) =>
            byName.Values.Where(predicate).Select(d => d.Name).ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>Every permission, by name.</summary>
    public IReadOnlyList<PermissionDefinition> Definitions { get; }

    /// <summary>Whether <paramref name="name"/> is declared.</summary>
    public bool Contains(string name) => Definitions.Any(d => d.Name == name);

    /// <summary>The union of what <paramref name="roleKeys"/> grant. Unknown keys grant nothing.</summary>
    public IReadOnlySet<string> PermissionsFor(IEnumerable<string> roleKeys)
    {
        ArgumentNullException.ThrowIfNull(roleKeys);

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roleKeys)
        {
            if (_byRole.TryGetValue(role, out var granted))
            {
                permissions.UnionWith(granted);
            }
        }

        return permissions;
    }
}

/// <summary>The permissions of the current request: the active membership's roles, resolved with its tenant.</summary>
public interface IPermissionContext
{
    /// <summary>Granted permissions; empty without an active membership.</summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>Whether <paramref name="permission"/> is granted.</summary>
    bool Has(string permission);
}

/// <summary>Mutable <see cref="IPermissionContext"/>, set once per request by the tenant resolver. Scoped.</summary>
public sealed class PermissionContext : IPermissionContext
{
    private static readonly IReadOnlySet<string> None = FrozenSet<string>.Empty;

    /// <inheritdoc />
    public IReadOnlySet<string> Permissions { get; private set; } = None;

    /// <inheritdoc />
    public bool Has(string permission) => Permissions.Contains(permission);

    /// <summary>Sets the request's permissions; only the tenant resolver (and tests) call this.</summary>
    public void Set(IReadOnlySet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        Permissions = permissions;
    }
}

/// <summary>Registration of permissions and the permission context.</summary>
public static class PermissionServiceCollectionExtensions
{
    /// <summary>Declares a module's permissions; call from <c>IModule.AddServices</c>.</summary>
    public static IServiceCollection AddPermissions(this IServiceCollection services, params IEnumerable<PermissionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        foreach (var definition in definitions)
        {
            services.AddSingleton(definition);
        }

        return services;
    }

    /// <summary>The catalog and the scoped <see cref="IPermissionContext"/> and <see cref="IMfaContext"/>.</summary>
    public static IServiceCollection AddShelterPermissions(this IServiceCollection services)
    {
        services.AddSingleton<PermissionCatalog>();
        services.AddScoped<PermissionContext>();
        services.AddScoped<IPermissionContext>(sp => sp.GetRequiredService<PermissionContext>());
        services.AddScoped<MfaContext>();
        services.AddScoped<IMfaContext>(sp => sp.GetRequiredService<MfaContext>());
        return services;
    }
}
