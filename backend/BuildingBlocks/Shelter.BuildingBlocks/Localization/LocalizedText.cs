namespace Shelter.BuildingBlocks.Localization;

/// <summary>
/// A value stored in both supported languages (ADR 0013). Both are required: reference data is always bilingual.
/// Clients pick the label by UI locale.
/// </summary>
/// <remarks>Mapped to two columns with <see cref="LocalizedTextMappingExtensions.MapLocalizedText{TEntity}"/>.</remarks>
public sealed record LocalizedText
{
    /// <summary>Default locale (architecture §11.1).</summary>
    public const string French = "fr-CA";

    /// <summary>Second supported locale.</summary>
    public const string English = "en-CA";

    public LocalizedText(string fr, string en)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fr);
        ArgumentException.ThrowIfNullOrWhiteSpace(en);

        Fr = fr;
        En = en;
    }

    /// <summary>fr-CA text.</summary>
    public string Fr { get; }

    /// <summary>en-CA text.</summary>
    public string En { get; }

    /// <summary>The text for <paramref name="locale"/> (<c>en</c>, <c>en-CA</c>, …); French for anything else.</summary>
    public string Resolve(string? locale) =>
        locale is not null
        && (locale.Equals("en", StringComparison.OrdinalIgnoreCase) || locale.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            ? En
            : Fr;
}
