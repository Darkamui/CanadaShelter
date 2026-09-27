using System.Text.RegularExpressions;

namespace Shelter.BuildingBlocks.Persistence.Migrations;

/// <summary>Validates and quotes the identifiers migration helpers interpolate into SQL.</summary>
public static partial class SqlIdentifier
{
    /// <summary>A double-quoted identifier; only lowercase snake_case names are accepted.</summary>
    public static string Quote(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        if (!SnakeCase().IsMatch(identifier))
        {
            throw new ArgumentException($"'{identifier}' is not a lowercase snake_case identifier.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }

    /// <summary><c>"schema"."table"</c>.</summary>
    public static string Qualified(string schema, string table) => $"{Quote(schema)}.{Quote(table)}";

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex SnakeCase();
}
