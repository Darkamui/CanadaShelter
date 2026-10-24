using Shelter.BuildingBlocks.Search;

namespace Shelter.BuildingBlocks.Tests.Search;

public sealed class SearchNormalizerTests
{
    [Theory]
    [InlineData("Éclair", "eclair")]
    [InlineData("ÉCLAIR", "eclair")]
    [InlineData("  Jean-François   Côté ", "jean francois cote")]
    [InlineData("Cœur de Lion", "coeur de lion")]
    [InlineData("Æsop", "aesop")]
    [InlineData("O'Neil", "o neil")]
    [InlineData("Noël 2", "noel 2")]
    public void Text_removes_accents_case_and_punctuation(string input, string expected) =>
        Assert.Equal(expected, SearchNormalizer.Text(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Text_of_nothing_is_empty(string? input) => Assert.Equal(string.Empty, SearchNormalizer.Text(input));

    [Theory]
    [InlineData("(514) 555-0199", "5145550199")]
    [InlineData("+1 514.555.0199 poste 2", "151455501992")]
    [InlineData(null, "")]
    public void Digits_keeps_only_ascii_digits(string? input, string expected) =>
        Assert.Equal(expected, SearchNormalizer.Digits(input));

    [Theory]
    [InlineData("985 112-000 123 456", "985112000123456")]
    [InlineData("qc-2026/abc", "QC2026ABC")]
    [InlineData(null, "")]
    public void Identifier_keeps_uppercase_alphanumerics(string? input, string expected) =>
        Assert.Equal(expected, SearchNormalizer.Identifier(input));

    [Fact]
    public void Contains_pattern_escapes_like_wildcards() =>
        Assert.Equal("%50\\%\\_off\\\\%", SearchNormalizer.ContainsPattern("50%_off\\"));
}
