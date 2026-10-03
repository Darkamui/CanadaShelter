using System.Globalization;
using System.Text;

namespace Shelter.BuildingBlocks.Search;

/// <summary>
/// Normalizes text for the stored search columns (architecture §12), so "Éclair", "eclair" and "ÉCLAIR" match without
/// database-side <c>unaccent</c>. The same function fills the column and normalizes the query, so they always agree.
/// Language-specific full-text search (ADR 0014) is a later, separate step.
/// </summary>
public static class SearchNormalizer
{
    /// <summary>
    /// Lower case, no accents, ligatures expanded (œ → oe, æ → ae), punctuation other than letters and digits turned
    /// into spaces, spaces collapsed. <c>null</c> or blank gives an empty string.
    /// </summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (!char.IsLetterOrDigit(lower))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            _ = lower switch
            {
                'œ' => builder.Append("oe"),
                'æ' => builder.Append("ae"),
                'ß' => builder.Append("ss"),
                _ => builder.Append(lower),
            };
        }

        return builder.ToString();
    }

    /// <summary>Digits only, for phone numbers: "(514) 555-0199" → "5145550199".</summary>
    public static string Digits(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>ASCII letters and digits only, upper case, for microchips and licences: "985 112-abc" → "985112ABC".</summary>
    public static string Identifier(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant));

    /// <summary>
    /// A <c>LIKE</c> pattern that matches <paramref name="normalized"/> anywhere, with <c>%</c>, <c>_</c> and
    /// <c>\</c> escaped (PostgreSQL's default escape character). Use with an already normalized value.
    /// </summary>
    public static string ContainsPattern(string normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        var escaped = normalized.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
